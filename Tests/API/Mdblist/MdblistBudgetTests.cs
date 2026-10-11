using System.Net;
using System.Net.Http.Headers;
using API.Http.Lanes;
using API.Mdblist;
using Shared.Concurrency;
using TestInfrastructure;

namespace API.Tests.Mdblist;

public class MdblistBudgetTests
{
	private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch.AddDays(1);
	private static readonly DateTimeOffset Reset = DateTimeOffset.UnixEpoch.AddDays(2);
	private static readonly DateTimeOffset LastReset = Now.AddHours(-1);

	[Test]
	public async Task BlockedUntil_ForATitleOnceTheReserveIsReached_IsTheResetAndWarnsOnce()
	{
		var logger = new CountingLogger<MdblistBudget>();
		var budget = new MdblistBudget(MdblistApis.Clock(), MdblistApis.Options, logger);
		budget.Record("mdblist-key", Headers(remaining: 120, Reset));

		var first = budget.BlockedUntil("mdblist-key", LaneClass.Title, Now);
		var second = budget.BlockedUntil("mdblist-key", LaneClass.Title, Now);

		await Assert.That(first).IsSome().And.IsEqualTo(Reset);
		await Assert.That(second).IsSome();
		await Assert.That(logger.Warnings).IsEqualTo(1);
	}

	[Test]
	public async Task BlockedUntil_ForATitleWhileRequestsAreLeftAboveTheReserve_IsNoneAndWarnsNothing()
	{
		var logger = new CountingLogger<MdblistBudget>();
		var budget = new MdblistBudget(MdblistApis.Clock(), MdblistApis.Options, logger);
		budget.Record("mdblist-key", Headers(remaining: 121, Reset));

		await Assert.That(budget.BlockedUntil("mdblist-key", LaneClass.Title, Now)).IsNone();
		await Assert.That(logger.Warnings).IsEqualTo(0);
	}

	[Test]
	public async Task BlockedUntil_ForInteractive_StopsAtTheFeedReserve()
	{
		var budget = MdblistApis.Budget();
		budget.Record("mdblist-key", Headers(remaining: 120, Reset));

		await Assert.That(budget.BlockedUntil("mdblist-key", LaneClass.Interactive, Now)).IsSome().And.IsEqualTo(Reset);
	}

	[Test]
	public async Task BlockedUntil_ForAListPage_PassesEvenAtTheFeedReserve()
	{
		var budget = MdblistApis.Budget();
		budget.Record("mdblist-key", Headers(remaining: 1, Reset));

		await Assert.That(budget.BlockedUntil("mdblist-key", LaneClass.Source, Now)).IsNone();
	}

	[Test]
	public async Task BlockedUntil_ForBackgroundWithLessThanHalfTheAllowanceLeft_BlocksUntilTheReset()
	{
		var budget = MdblistApis.Budget();
		budget.Record("mdblist-key", Headers(remaining: 30, LastReset));
		budget.Record("mdblist-key", Headers(remaining: 1000, Reset));
		budget.Record("mdblist-key", Headers(remaining: 499, Reset));

		await Assert.That(budget.BlockedUntil("mdblist-key", LaneClass.Background, Now)).IsSome().And.IsEqualTo(Reset);
		await Assert.That(budget.BlockedUntil("mdblist-key", LaneClass.Title, Now)).IsNone();
	}

	[Test]
	public async Task BlockedUntil_ForBackgroundWithHalfTheAllowanceLeft_Passes()
	{
		var budget = MdblistApis.Budget();
		budget.Record("mdblist-key", Headers(remaining: 30, LastReset));
		budget.Record("mdblist-key", Headers(remaining: 1000, Reset));
		budget.Record("mdblist-key", Headers(remaining: 500, Reset));

		await Assert.That(budget.BlockedUntil("mdblist-key", LaneClass.Background, Now)).IsNone();
	}

	[Test]
	public async Task BlockedUntil_ForBackgroundBeforeAnyAnswerSinceTheReset_StopsAtTheFeedReserve()
	{
		var budget = MdblistApis.Budget();
		budget.Record("mdblist-key", Headers(remaining: 300, Reset));

		budget.Record("mdblist-key", Headers(remaining: 121, Reset));
		await Assert.That(budget.BlockedUntil("mdblist-key", LaneClass.Background, Now)).IsNone();
		budget.Record("mdblist-key", Headers(remaining: 120, Reset));
		await Assert.That(budget.BlockedUntil("mdblist-key", LaneClass.Background, Now)).IsSome().And.IsEqualTo(Reset);
	}

	[Test]
	public async Task BlockedUntil_ForBackgroundWhenTheResetDriftsBySeconds_StillCountsTheWindowsAllowance()
	{
		var budget = MdblistApis.Budget();
		budget.Record("mdblist-key", Headers(remaining: 30, LastReset));
		budget.Record("mdblist-key", Headers(remaining: 1000, Reset));
		budget.Record("mdblist-key", Headers(remaining: 499, Reset.AddSeconds(2)));

		await Assert.That(budget.BlockedUntil("mdblist-key", LaneClass.Background, Now)).IsSome();
	}

	[Test]
	public async Task BlockedUntil_ForBackground_NeverSpendsTheFeedReserve()
	{
		var budget = MdblistApis.Budget();
		budget.Record("mdblist-key", Headers(remaining: 30, LastReset));
		budget.Record("mdblist-key", Headers(remaining: 200, Reset));
		budget.Record("mdblist-key", Headers(remaining: 110, Reset));

		await Assert.That(budget.BlockedUntil("mdblist-key", LaneClass.Background, Now)).IsSome().And.IsEqualTo(Reset);
	}

	[Test]
	public async Task Record_ForARequestStampedWithTheLaneOwner_CountsItForThatKey()
	{
		var budget = MdblistApis.Budget();
		var answered = new HttpResponseMessage(HttpStatusCode.OK);
		answered.Headers.Add("x-ratelimit-remaining", "640");
		answered.Headers.Add("x-ratelimit-reset", $"{Reset.ToUnixTimeSeconds()}");
		var wire = Mock.HttpHandler();
		wire.OnAnyRequest().RespondWith(answered);
		using var invoker = new HttpMessageInvoker(new MdblistBudgetHandler(budget) { InnerHandler = wire });
		using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.mdblist.com/lists/1/items");
		request.Options.Set(LaneHandler.Owner, "mdblist-key");

		using var response = await invoker.SendAsync(request, CancellationToken.None);

		await Assert.That(budget.Left("mdblist-key")).IsSome().And.IsEqualTo(640);
	}

	[Test]
	public async Task Left_BeforeTheReset_IsWhatMdblistLastSaid()
	{
		var budget = MdblistApis.Budget();
		budget.Record("mdblist-key", Headers(remaining: 962, Reset));

		await Assert.That(budget.Left("mdblist-key")).IsSome().And.IsEqualTo(962);
		await Assert.That(budget.Left("another-key")).IsNone();
	}

	[Test]
	public async Task Left_AfterTheReset_IsUnknown()
	{
		var budget = MdblistApis.Budget();
		budget.Record("mdblist-key", Headers(remaining: 12, DateTimeOffset.UnixEpoch.AddHours(12)));

		await Assert.That(budget.Left("mdblist-key")).IsNone();
	}

	private static HttpResponseHeaders Headers(long remaining, DateTimeOffset reset)
	{
		var response = new HttpResponseMessage(HttpStatusCode.OK);
		response.Headers.Add("x-ratelimit-remaining", $"{remaining}");
		response.Headers.Add("x-ratelimit-reset", $"{reset.ToUnixTimeSeconds()}");
		return response.Headers;
	}
}
