using Contracts.Updates;
using Microsoft.Extensions.Time.Testing;
using Shared.Options;
using TestInfrastructure;
using UI.Shared.Services;

namespace UI.Tests.Settings.About;

public class AppUpdateCheckerTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);

	private static readonly Uri Page = new("https://github.com/daerup/Sammlarr/releases/tag/v1.5.0");

	private readonly Mock<IReleaseFeed> _releases = IReleaseFeed.Mock();

	private readonly FakeTimeProvider _clock = new(Now);

	public AppUpdateCheckerTests()
	{
		_releases.LatestAsync(Any(), Any()).Returns(Some.Of(new PublishedRelease("v1.5.0", Page)));
	}

	[Test]
	public async Task CheckAsync_ForANewerRelease_ReportsItsVersionAndPage()
	{
		var update = await Testee("1.4.0").CheckAsync();

		await Assert.That(update).IsSome().And.IsEqualTo(new AppUpdate("1.5.0", Page));
	}

	[Test]
	public async Task CheckAsync_ForAReleaseWhoseMinorVersionRunsPastNine_ComparesItAsANumber()
	{
		_releases.LatestAsync(Any(), Any()).Returns(Some.Of(new PublishedRelease("v1.10.0", Page)));

		await Assert.That(await Testee("1.9.0").CheckAsync()).IsSome().And.IsEqualTo(new AppUpdate("1.10.0", Page));
	}

	[Test]
	[Arguments("v1.4.0")]
	[Arguments("v1.3.9")]
	[Arguments("v1.5.0-rc.1")]
	[Arguments("nightly")]
	public async Task CheckAsync_ForATagThatIsNoNewerRelease_ReportsNothing(string tag)
	{
		_releases.LatestAsync(Any(), Any()).Returns(Some.Of(new PublishedRelease(tag, Page)));

		await Assert.That(await Testee("1.4.0").CheckAsync()).IsNone();
	}

	[Test]
	public async Task CheckAsync_WhenGitHubGivesNoRelease_ReportsNothing()
	{
		_releases.LatestAsync(Any(), Any()).Returns(new None());

		await Assert.That(await Testee("1.4.0").CheckAsync()).IsNone();
	}

	[Test]
	public async Task CheckAsync_TwiceWithinHalfAnHour_AsksGitHubOnce()
	{
		var testee = Testee("1.4.0");
		await testee.CheckAsync();
		_clock.SetUtcNow(Now.AddMinutes(29));

		await testee.CheckAsync();

		_releases.LatestAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task CheckAsync_AfterHalfAnHour_AsksGitHubAgain()
	{
		var testee = Testee("1.4.0");
		await testee.CheckAsync();
		_clock.SetUtcNow(Now.AddMinutes(30));

		await testee.CheckAsync();

		_releases.LatestAsync(Any(), Any()).WasCalled(Times.Exactly(2));
	}

	[Test]
	public async Task CheckAsync_WithAConfiguredInterval_AsksGitHubAgainOnlyOnceItPassed()
	{
		var testee = Testee("1.4.0", new ReleaseOptions { CheckInterval = TimeSpan.FromHours(2) });
		await testee.CheckAsync();
		_clock.SetUtcNow(Now.AddMinutes(90));
		await testee.CheckAsync();
		_clock.SetUtcNow(Now.AddHours(2));

		await testee.CheckAsync();

		_releases.LatestAsync(Any(), Any()).WasCalled(Times.Exactly(2));
	}

	[Test]
	public async Task CheckNowAsync_WithinHalfAnHour_AsksGitHubAgain()
	{
		var testee = Testee("1.4.0");
		await testee.CheckAsync();

		await testee.CheckNowAsync();

		_releases.LatestAsync(Any(), Any()).WasCalled(Times.Exactly(2));
	}

	[Test]
	public async Task CheckAsync_AfterCheckNow_ReportsItsNewerAnswer()
	{
		var testee = Testee("1.4.0");
		_releases.LatestAsync(Any(), Any()).Returns(new None());
		await testee.CheckAsync();
		_releases.LatestAsync(Any(), Any()).Returns(Some.Of(new PublishedRelease("v1.5.0", Page)));
		await testee.CheckNowAsync();

		var update = await testee.CheckAsync();

		await Assert.That(update).IsSome().And.IsEqualTo(new AppUpdate("1.5.0", Page));
		_releases.LatestAsync(Any(), Any()).WasCalled(Times.Exactly(2));
	}

	[Test]
	public async Task CheckAsync_TwiceBeforeGitHubAnswers_AsksGitHubOnce()
	{
		var answer = new TaskCompletionSource<Optional<PublishedRelease>>();
		_releases.LatestAsync(Any(), Any()).ReturnsAsync(answer.Task);
		var testee = Testee("1.4.0");

		var first = testee.CheckAsync();
		var second = testee.CheckAsync();
		answer.SetResult(new None());
		await Task.WhenAll(first, second);

		_releases.LatestAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	[Arguments("0.0.0-alpha.0.584")]
	[Arguments("0.0.0-local")]
	[Arguments("1.4.1-alpha.0.3")]
	[Arguments("2.0.0-rc.1")]
	public async Task CheckAsync_ForABuildThatIsNoRelease_NeverAsksGitHub(string running)
	{
		var update = await Testee(running).CheckAsync();

		await Assert.That(update).IsNone();
		_releases.LatestAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task CheckAsync_WithoutARepository_NeverAsksGitHub()
	{
		var update = await Testee("1.4.0", new ReleaseOptions { GitHubRepo = string.Empty }).CheckAsync();

		await Assert.That(update).IsNone();
		_releases.LatestAsync(Any(), Any()).WasNeverCalled();
	}

	private AppUpdateChecker Testee(string running, ReleaseOptions? options = null)
	{
		var release = options ?? new ReleaseOptions();
		return new AppUpdateChecker(new AppVersionProvider(release, new AppVersion(running)), release, _releases.Object, _clock);
	}
}
