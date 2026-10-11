using API.Mdblist;
using Contracts.Auth;
using Domain.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using TestInfrastructure;

namespace API.Tests.Mdblist;

public class MdblistRequestsLeftTests
{
	private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch.AddDays(1);

	private readonly Mock<IApiKeyConfigRepository> _keys = IApiKeyConfigRepository.Mock();

	private readonly MdblistBudget _budget = MdblistApis.Budget();

	[Test]
	public async Task TodayAsync_WithoutAKey_IsNone()
	{
		_keys.GetAsync(ApiKeyProvider.Mdblist, Any()).Returns(Error.From("No API key is configured for this provider."));

		await Assert.That(await Testee().TodayAsync()).IsNone();
	}

	[Test]
	public async Task TodayAsync_WithAKeyButNoAnswerYet_TakesTheNextResetFromTheDailyTime()
	{
		_keys.GetAsync(ApiKeyProvider.Mdblist, Any()).Returns(Success.Of("mdblist-key"));

		var today = await Assert.That(await Testee().TodayAsync()).IsSome();

		await Assert.That(today!.Answered).IsNone();
		await Assert.That(today.NextReset).IsEqualTo(Now.AddDays(1));
	}

	[Test]
	public async Task TodayAsync_WithAnAnswerFromBeforeTheReset_SaysOnlyWhenTheNextResetIs()
	{
		_keys.GetAsync(ApiKeyProvider.Mdblist, Any()).Returns(Success.Of("mdblist-key"));
		_budget.Record("mdblist-key", MdblistApis.RateLimited(12, Now.AddHours(-12)));

		var today = await Assert.That(await Testee().TodayAsync()).IsSome();

		await Assert.That(today!.Answered).IsNone();
		await Assert.That(today.NextReset).IsEqualTo(Now.AddHours(12));
	}

	[Test]
	public async Task TodayAsync_WithAnAnswerSinceTheReset_CarriesIt()
	{
		_keys.GetAsync(ApiKeyProvider.Mdblist, Any()).Returns(Success.Of("mdblist-key"));
		_budget.Record("mdblist-key", MdblistApis.RateLimited(962, Now.AddHours(5)));

		var today = await Assert.That(await Testee().TodayAsync()).IsSome();

		var answered = await Assert.That(today!.Answered).IsSome();
		await Assert.That(answered!.Remaining).IsEqualTo(962);
		await Assert.That(today.NextReset).IsEqualTo(Now.AddHours(5));
	}

	[Test]
	public async Task TodayAsync_WithAKeyButNoAnswerYet_TakesTheNextResetFromTheConfiguredTime()
	{
		_keys.GetAsync(ApiKeyProvider.Mdblist, Any()).Returns(Success.Of("mdblist-key"));
		var budget = new MdblistBudget(MdblistApis.Clock(), MdblistApis.Options with { DailyResetUtc = new TimeOnly(2, 0) }, NullLogger<MdblistBudget>.Instance);

		var today = await Assert.That(await new MdblistRequestsLeft(_keys.Object, budget).TodayAsync()).IsSome();

		await Assert.That(today!.NextReset).IsEqualTo(Now.AddHours(2));
	}

	private MdblistRequestsLeft Testee() => new(_keys.Object, _budget);
}
