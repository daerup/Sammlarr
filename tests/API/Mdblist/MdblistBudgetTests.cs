using System.Net;
using API.Mdblist;
using Contracts;
using Domain.Providers;
using Domain.Usage;
using Microsoft.Extensions.Logging.Abstractions;
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
		budget.Record("mdblist-key", Answer(remaining: 120, Reset));

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
		budget.Record("mdblist-key", Answer(remaining: 121, Reset));

		await Assert.That(budget.BlockedUntil("mdblist-key", LaneClass.Title, Now)).IsNone();
		await Assert.That(logger.Warnings).IsEqualTo(0);
	}

	[Test]
	public async Task BlockedUntil_ForInteractive_StopsAtTheFeedReserve()
	{
		var budget = MdblistApis.Budget();
		budget.Record("mdblist-key", Answer(remaining: 120, Reset));

		await Assert.That(budget.BlockedUntil("mdblist-key", LaneClass.Interactive, Now)).IsSome().And.IsEqualTo(Reset);
	}

	[Test]
	public async Task BlockedUntil_ForAListPage_PassesEvenAtTheFeedReserve()
	{
		var budget = MdblistApis.Budget();
		budget.Record("mdblist-key", Answer(remaining: 1, Reset));

		await Assert.That(budget.BlockedUntil("mdblist-key", LaneClass.Source, Now)).IsNone();
	}

	[Test]
	public async Task BlockedUntil_ForBackgroundWithLessThanHalfTheAllowanceLeft_BlocksUntilTheReset()
	{
		var budget = MdblistApis.Budget();
		budget.Record("mdblist-key", Answer(remaining: 30, LastReset));
		budget.Record("mdblist-key", Answer(remaining: 1000, Reset));
		budget.Record("mdblist-key", Answer(remaining: 499, Reset));

		await Assert.That(budget.BlockedUntil("mdblist-key", LaneClass.Background, Now)).IsSome().And.IsEqualTo(Reset);
		await Assert.That(budget.BlockedUntil("mdblist-key", LaneClass.Title, Now)).IsNone();
	}

	[Test]
	public async Task BlockedUntil_ForBackgroundWithHalfTheAllowanceLeft_Passes()
	{
		var budget = MdblistApis.Budget();
		budget.Record("mdblist-key", Answer(remaining: 30, LastReset));
		budget.Record("mdblist-key", Answer(remaining: 1000, Reset));
		budget.Record("mdblist-key", Answer(remaining: 500, Reset));

		await Assert.That(budget.BlockedUntil("mdblist-key", LaneClass.Background, Now)).IsNone();
	}

	[Test]
	public async Task BlockedUntil_ForBackgroundBeforeAnyAnswerSinceTheReset_StopsAtTheFeedReserve()
	{
		var budget = MdblistApis.Budget();
		budget.Record("mdblist-key", Answer(remaining: 300, Reset));

		budget.Record("mdblist-key", Answer(remaining: 121, Reset));
		await Assert.That(budget.BlockedUntil("mdblist-key", LaneClass.Background, Now)).IsNone();
		budget.Record("mdblist-key", Answer(remaining: 120, Reset));
		await Assert.That(budget.BlockedUntil("mdblist-key", LaneClass.Background, Now)).IsSome().And.IsEqualTo(Reset);
	}

	[Test]
	public async Task BlockedUntil_ForBackgroundWhenTheResetDriftsBySeconds_StillCountsTheWindowsAllowance()
	{
		var budget = MdblistApis.Budget();
		budget.Record("mdblist-key", Answer(remaining: 30, LastReset));
		budget.Record("mdblist-key", Answer(remaining: 1000, Reset));
		budget.Record("mdblist-key", Answer(remaining: 499, Reset.AddSeconds(2)));

		await Assert.That(budget.BlockedUntil("mdblist-key", LaneClass.Background, Now)).IsSome();
	}

	[Test]
	public async Task BlockedUntil_ForBackground_NeverSpendsTheFeedReserve()
	{
		var budget = MdblistApis.Budget();
		budget.Record("mdblist-key", Answer(remaining: 30, LastReset));
		budget.Record("mdblist-key", Answer(remaining: 200, Reset));
		budget.Record("mdblist-key", Answer(remaining: 110, Reset));

		await Assert.That(budget.BlockedUntil("mdblist-key", LaneClass.Background, Now)).IsSome().And.IsEqualTo(Reset);
	}

	[Test]
	public async Task Today_BeforeTheReset_AnswersWhatMdblistLastSaid()
	{
		var budget = MdblistApis.Budget();
		budget.Record("mdblist-key", Answer(remaining: 962, Reset));

		await Assert.That(Left(budget, "mdblist-key")).IsSome().And.IsEqualTo(962);
		await Assert.That(Left(budget, "another-key")).IsNone();
	}

	[Test]
	public async Task Today_AfterTheReset_HasNoAnswerAndStepsTheResetOnByADay()
	{
		var budget = MdblistApis.Budget();
		budget.Record("mdblist-key", Answer(remaining: 12, DateTimeOffset.UnixEpoch.AddHours(12)));

		var today = budget.Today("mdblist-key");

		await Assert.That(today.Answered).IsNone();
		await Assert.That(today.NextReset).IsEqualTo(DateTimeOffset.UnixEpoch.AddHours(36));
	}

	[Test]
	public async Task Today_AfterAForeignDrop_SaysWhatWasSpentOutside()
	{
		var budget = MdblistApis.Budget();
		budget.Record("mdblist-key", Answer(remaining: 30, LastReset, limit: 1000));
		budget.Record("mdblist-key", Answer(remaining: 999, Reset, limit: 1000));
		budget.Record("mdblist-key", Answer(remaining: 998, Reset, limit: 1000));
		budget.Record("mdblist-key", Answer(remaining: 880, Reset, limit: 1000));

		var answered = await Assert.That(budget.Today("mdblist-key").Answered).IsSome();

		await Assert.That(answered!.Outside).IsSome().And.IsEqualTo(117);
		await Assert.That(answered.Allowance).IsSome().And.IsEqualTo(1000);
	}

	[Test]
	public async Task Today_WithoutTheLimitHeader_TakesTheAllowanceAndWhatWasSpentOutsideFromTheFirstReading()
	{
		var budget = MdblistApis.Budget();
		budget.Record("mdblist-key", Answer(remaining: 30, LastReset));
		budget.Record("mdblist-key", Answer(remaining: 999, Reset));
		budget.Record("mdblist-key", Answer(remaining: 998, Reset));
		budget.Record("mdblist-key", Answer(remaining: 880, Reset));

		var answered = await Assert.That(budget.Today("mdblist-key").Answered).IsSome();

		await Assert.That(answered!.Outside).IsSome().And.IsEqualTo(117);
		await Assert.That(answered.Allowance).IsSome().And.IsEqualTo(1000);
	}

	[Test]
	public async Task Today_ForOwnAnswersOnly_SaysNothingWasSpentOutside()
	{
		var budget = MdblistApis.Budget();
		budget.Record("mdblist-key", Answer(remaining: 30, LastReset, limit: 1000));
		budget.Record("mdblist-key", Answer(remaining: 999, Reset, limit: 1000));
		budget.Record("mdblist-key", Answer(remaining: 998, Reset, limit: 1000));

		var answered = await Assert.That(budget.Today("mdblist-key").Answered).IsSome();

		await Assert.That(answered!.Outside).IsNone();
	}

	[Test]
	public async Task Today_AfterARestart_KnowsTheAllowanceButNotWhatWasSpentOutside()
	{
		var budget = MdblistApis.Budget();
		budget.Record("mdblist-key", Answer(remaining: 500, Reset, limit: 1000));

		var answered = await Assert.That(budget.Today("mdblist-key").Answered).IsSome();

		await Assert.That(answered!.Allowance).IsSome().And.IsEqualTo(1000);
		await Assert.That(answered.Outside).IsNone();
	}

	[Test]
	public async Task Today_WhenAReadingCrossesTheReserve_StampsThatMinuteOnly()
	{
		var clock = IDateTimeProvider.Mock();
		clock.Now.ReturnsSequentially([Now, Now, Now.AddMinutes(5), Now.AddMinutes(9), Now.AddMinutes(9)]);
		var budget = new MdblistBudget(clock.Object, MdblistApis.Options, NullLogger<MdblistBudget>.Instance);
		budget.Record("mdblist-key", Answer(remaining: 200, Reset));
		budget.Record("mdblist-key", Answer(remaining: 121, Reset));
		var before = budget.Today("mdblist-key").Answered.Map(answered => answered.ReserveReachedAt);
		budget.Record("mdblist-key", Answer(remaining: 119, Reset));
		budget.Record("mdblist-key", Answer(remaining: 118, Reset));

		var answered = await Assert.That(budget.Today("mdblist-key").Answered).IsSome();

		await Assert.That(before).IsNone();
		await Assert.That(answered!.ReserveReachedAt).IsSome().And.IsEqualTo(Now.AddMinutes(5));
	}

	[Test]
	public async Task Today_ForAFirstReadingAlreadyBelowTheReserve_StampsNothing()
	{
		var budget = MdblistApis.Budget();
		budget.Record("mdblist-key", Answer(remaining: 100, Reset));
		budget.Record("mdblist-key", Answer(remaining: 99, Reset));

		var answered = await Assert.That(budget.Today("mdblist-key").Answered).IsSome();

		await Assert.That(answered!.ReserveReachedAt).IsNone();
	}

	[Test]
	public async Task Record_ForALateAnswerFromTheDayBefore_ChangesAndCountsNothing()
	{
		var budget = MdblistApis.Budget();
		budget.Record("mdblist-key", Answer(remaining: 30, LastReset, limit: 1000));
		budget.Record("mdblist-key", Answer(remaining: 900, Reset, limit: 1000));
		budget.Record("mdblist-key", Answer(remaining: 20, LastReset, limit: 1000));

		var answered = await Assert.That(budget.Today("mdblist-key").Answered).IsSome();

		await Assert.That((answered!.Remaining, answered.ResetAt)).IsEqualTo((900L, Reset));
		await Assert.That(answered.Outside).IsSome().And.IsEqualTo(99);
	}

	[Test]
	public async Task Record_ForARefusedAnswer_CountsNoRequest()
	{
		var budget = MdblistApis.Budget();
		budget.Record("mdblist-key", Answer(remaining: 30, LastReset, limit: 1000));
		budget.Record("mdblist-key", Answer(remaining: 999, Reset, limit: 1000));
		budget.Record("mdblist-key", Answer(remaining: 880, Reset, limit: 1000));
		budget.Record("mdblist-key", Answer(remaining: 880, Reset, limit: 1000, HttpStatusCode.TooManyRequests));

		var answered = await Assert.That(budget.Today("mdblist-key").Answered).IsSome();

		await Assert.That(answered!.Outside).IsSome().And.IsEqualTo(118);
	}

	[Test]
	public async Task Today_AfterARestartWithoutTheLimitHeader_KnowsNoAllowance()
	{
		var budget = MdblistApis.Budget();
		budget.Record("mdblist-key", Answer(remaining: 500, Reset));

		var answered = await Assert.That(budget.Today("mdblist-key").Answered).IsSome();

		await Assert.That(answered!.Allowance).IsNone();
	}

	[Test]
	public async Task Today_AfterANewReset_KeepsHowTheDayBeforeEnded()
	{
		var budget = MdblistApis.Budget();
		budget.Record("mdblist-key", Answer(remaining: 200, LastReset));
		budget.Record("mdblist-key", Answer(remaining: 100, LastReset));
		budget.Record("mdblist-key", Answer(remaining: 999, Reset));
		budget.Record("mdblist-key", Answer(remaining: 998, Reset));
		budget.Record("mdblist-key", Answer(remaining: 20, LastReset));

		var answered = await Assert.That(budget.Today("mdblist-key").Answered).IsSome();

		await Assert.That(answered!.DayBefore).IsSome().And.IsEqualTo(new DayEnd(100, LastReset, Some.Of(Now)));
	}

	[Test]
	public async Task Today_AfterARestart_KnowsNoDayBefore()
	{
		var budget = MdblistApis.Budget();
		budget.Record("mdblist-key", Answer(remaining: 999, Reset));

		var answered = await Assert.That(budget.Today("mdblist-key").Answered).IsSome();

		await Assert.That(answered!.DayBefore).IsNone();
	}

	[Test]
	public async Task Today_AfterANewReset_StartsTheDayAfresh()
	{
		var budget = MdblistApis.Budget();
		budget.Record("mdblist-key", Answer(remaining: 200, LastReset, limit: 1000));
		budget.Record("mdblist-key", Answer(remaining: 100, LastReset, limit: 1000));
		budget.Record("mdblist-key", Answer(remaining: 999, Reset, limit: 1000));

		var answered = await Assert.That(budget.Today("mdblist-key").Answered).IsSome();

		await Assert.That(answered!.ReserveReachedAt).IsNone();
		await Assert.That(answered.Outside).IsNone();
	}

	private static Optional<long> Left(MdblistBudget budget, string apiKey) => budget.Today(apiKey).Answered.Map(answered => answered.Remaining);

	private static HttpResponseMessage Answer(long remaining, DateTimeOffset reset, long? limit = null, HttpStatusCode status = HttpStatusCode.OK) =>
		MdblistApis.RateLimited(remaining, reset, limit, status);
}
