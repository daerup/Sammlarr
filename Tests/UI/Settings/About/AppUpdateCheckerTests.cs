using Contracts;
using Contracts.Updates;
using Shared.Options;
using TestInfrastructure;
using UI.Shared.Services;

namespace UI.Tests.Settings.About;

public class AppUpdateCheckerTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);

	private static readonly Uri Page = new("https://github.com/daerup/Sammlarr/releases/tag/v1.5.0");

	private readonly Mock<IReleaseFeed> _releases = IReleaseFeed.Mock();

	private readonly Mock<IDateTimeProvider> _clock = IDateTimeProvider.Mock();

	public AppUpdateCheckerTests()
	{
		_clock.Now.Returns(Now);
		_releases.LatestAsync(Any(), Any()).Returns(Some.Of(new PublishedRelease("v1.5.0", Page)));
	}

	[Test]
	public async Task CheckAsync_ForANewerRelease_ReportsItsVersionAndPage()
	{
		var update = await Testee("1.4.0").CheckAsync();

		await Assert.That(update).IsSome().And.IsEqualTo(new AppUpdate("1.5.0", Page));
	}

	[Test]
	[Arguments("v1.4.0")]
	[Arguments("v1.3.9")]
	public async Task CheckAsync_ForTheSameOrAnOlderRelease_ReportsNothing(string tag)
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
	public async Task CheckAsync_TwiceWithinHalfADay_AsksGitHubOnce()
	{
		var testee = Testee("1.4.0");
		await testee.CheckAsync();
		_clock.Now.Returns(Now.AddHours(11));

		await testee.CheckAsync();

		_releases.LatestAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task CheckAsync_AfterHalfADay_AsksGitHubAgain()
	{
		var testee = Testee("1.4.0");
		await testee.CheckAsync();
		_clock.Now.Returns(Now.AddHours(12));

		await testee.CheckAsync();

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
	public async Task CheckAsync_ForABuildThatIsNoRelease_NeverAsksGitHub(string running)
	{
		var update = await Testee(running).CheckAsync();

		await Assert.That(update).IsNone();
		_releases.LatestAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task CheckAsync_WithoutARepository_NeverAsksGitHub()
	{
		var update = await Testee("1.4.0", repository: string.Empty).CheckAsync();

		await Assert.That(update).IsNone();
		_releases.LatestAsync(Any(), Any()).WasNeverCalled();
	}

	private AppUpdateChecker Testee(string running, string repository = "daerup/Sammlarr")
	{
		return new AppUpdateChecker(
			new AppVersionProvider(new ReleaseOptions { GitHubRepo = repository }, new AppVersion(running)),
			_releases.Object,
			_clock.Object
		);
	}
}
