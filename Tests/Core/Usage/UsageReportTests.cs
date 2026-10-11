using Contracts;
using Contracts.Datasets;
using Contracts.Feed;
using Contracts.Providers;
using Contracts.Usage;
using Core.Usage;
using Domain.Feed;
using Domain.Identity;
using Domain.Providers;
using Domain.Usage;
using TestInfrastructure;
using TUnit.Assertions.Enums;

namespace Core.Tests.Usage;

public class UsageReportTests
{
	private static readonly DateTimeOffset Midnight = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);

	private static readonly DateTimeOffset Now = Midnight.AddHours(12.5);

	private readonly Mock<IUsageStore> _store = IUsageStore.Mock();

	private readonly Mock<IUsageLastCalls> _lastCalls = IUsageLastCalls.Mock();

	private readonly Mock<IRemoteCapacity> _capacity = IRemoteCapacity.Mock();

	private readonly Mock<IDatasetLoads> _loads = IDatasetLoads.Mock();

	private readonly Mock<IMdblistRequestsLeft> _mdblist = IMdblistRequestsLeft.Mock();

	private readonly Mock<IFeedService> _feeds = IFeedService.Mock();

	public UsageReportTests()
	{
		_lastCalls.Of(Any()).Returns(new None());
		_capacity.Of(Any()).Returns(RemoteLoad.Idle);
		_loads.FindAsync(Any(), Any()).Returns(new None());
		_mdblist.TodayAsync(Any()).Returns(new None());
		_feeds.GetAsync(Any()).Returns(new FeedSnapshot([]));
		_feeds.GetRunsAsync(Any(), Any()).Returns([]);
	}

	[Test]
	public async Task ReadAsync_ADay_CountsTheCallsOfEachHourAndOfTheDayBefore()
	{
		Stored(
			Calls(RemoteService.Tmdb, 11, UsageOutcome.Answered, 5),
			Calls(RemoteService.Tmdb, 12, UsageOutcome.Failed, 2),
			Calls(RemoteService.Tmdb, -20, UsageOutcome.Answered, 3)
		);

		var report = await Testee().ReadAsync(UsageRange.Day);

		await Assert
			.That(report.Current.Slots.Select(slot => slot.Tally.Total))
			.IsEquivalentTo([.. Enumerable.Repeat(0L, 22), 5L, 2L], CollectionOrdering.Matching);
		await Assert.That(report.Current.Tally.Errors).IsEqualTo(2);
		await Assert.That(report.Previous.Tally.Total).IsEqualTo(3);
	}

	[Test]
	public async Task ReadAsync_EachService_CountsOnlyItsOwnCallsOfEachHour()
	{
		Stored(
			Calls(RemoteService.Tmdb, 11, UsageOutcome.Answered, 5),
			Calls(RemoteService.Plex, 11, UsageOutcome.Answered, 4),
			Calls(RemoteService.Plex, 12, UsageOutcome.Failed, 2)
		);

		var services = (await Testee().ReadAsync(UsageRange.Day)).Current.Services.ToDictionary(line => line.Service, line => line.Slots);

		await Assert
			.That(services[RemoteService.Tmdb].Select(slot => slot.Tally.Total))
			.IsEquivalentTo([.. Enumerable.Repeat(0L, 22), 5L, 0L], CollectionOrdering.Matching);
		await Assert
			.That(services[RemoteService.Plex].Select(slot => slot.Tally.Total))
			.IsEquivalentTo([.. Enumerable.Repeat(0L, 22), 4L, 2L], CollectionOrdering.Matching);
	}

	[Test]
	public async Task ReadAsync_ManyCauses_ListsTheBusiestFirst()
	{
		Stored(
			Calls(RemoteService.Tmdb, 11, UsageOutcome.Answered, 1, UsageCause.Search),
			Calls(RemoteService.Plex, 11, UsageOutcome.Answered, 7),
			Calls(RemoteService.Tmdb, 10, UsageOutcome.Answered, 2)
		);

		var report = await Testee().ReadAsync(UsageRange.Day);

		await Assert
			.That(report.Current.Causes.Select(line => (line.Cause, line.Tally.Total)))
			.IsEquivalentTo([(UsageCause.FeedRun, 9L), (UsageCause.Search, 1L)], CollectionOrdering.Matching);
		await Assert
			.That(report.Current.Services.Select(line => line.Service))
			.IsEquivalentTo([RemoteService.Plex, RemoteService.Tmdb], CollectionOrdering.Matching);
	}

	[Test]
	public async Task ReadAsync_EachService_CountsItsOwnCausesAndEachHoursCauses()
	{
		Stored(
			[
				Calls(RemoteService.Tmdb, 11, UsageOutcome.Answered, 1, UsageCause.Search),
				Calls(RemoteService.Tmdb, 11, UsageOutcome.Answered, 3),
				Calls(RemoteService.Plex, 12, UsageOutcome.Answered, 7),
			],
			[Attempts(RemoteService.Tmdb, 11, 4, 250), Attempts(RemoteService.Plex, 12, 9, 1000)]
		);

		var tmdb = (await Testee().ReadAsync(UsageRange.Day)).Current.Services.Single(line => line.Service == RemoteService.Tmdb);

		await Assert
			.That(tmdb.Causes.Select(line => (line.Cause, line.Tally.Total)))
			.IsEquivalentTo([(UsageCause.FeedRun, 3L), (UsageCause.Search, 1L)], CollectionOrdering.Matching);
		await Assert.That(tmdb.Slots[22].Causes).IsEquivalentTo([KeyValuePair.Create(UsageCause.FeedRun, 3L), KeyValuePair.Create(UsageCause.Search, 1L)]);
		await Assert.That(tmdb.Slots[23].Causes).IsEmpty();
		await Assert.That(tmdb.Causes.Single(line => line.Cause == UsageCause.FeedRun).Tally.Durations).IsEquivalentTo([KeyValuePair.Create(250, 4L)]);
	}

	[Test]
	public async Task ReadAsync_AServiceThatStoppedAnswering_SaysSinceWhichHour()
	{
		Stored(
			Calls(RemoteService.Plex, 9, UsageOutcome.Answered, 4),
			Calls(RemoteService.Plex, 10, UsageOutcome.Failed, 3),
			Calls(RemoteService.Plex, 11, UsageOutcome.GaveUpOn429, 2),
			Calls(RemoteService.Plex, 11, UsageOutcome.Cancelled, 1)
		);

		var plex = (await Testee().ReadAsync(UsageRange.Day)).Current.Services.Single();

		await Assert.That(plex.RightNow.FailingSince).IsSome().And.IsEqualTo(Midnight.AddHours(10));
	}

	[Test]
	public async Task ReadAsync_AServiceAnsweringAgain_IsNotFailing()
	{
		Stored(Calls(RemoteService.Plex, 10, UsageOutcome.Failed, 3), Calls(RemoteService.Plex, 11, UsageOutcome.Answered, 1));

		var plex = (await Testee().ReadAsync(UsageRange.Day)).Current.Services.Single();

		await Assert.That(plex.RightNow.FailingSince).IsNone();
	}

	[Test]
	public async Task ReadAsync_TheLastCall_PrefersTheMinuteKeptSinceTheStart()
	{
		_lastCalls.Of(RemoteService.Plex).Returns(Some.Of(Now.AddMinutes(-9)));
		Stored(
			Calls(RemoteService.Plex, 11, UsageOutcome.Answered, 1),
			Calls(RemoteService.Tmdb, 9, UsageOutcome.Answered, 1),
			Calls(RemoteService.Plex, -20, UsageOutcome.Answered, 1)
		);

		var report = await Testee().ReadAsync(UsageRange.Day);

		var services = report.Current.Services;
		await Assert.That(services.Single(line => line.Service == RemoteService.Plex).RightNow.LastCall).IsSome().And.IsEqualTo(Now.AddMinutes(-9));
		await Assert.That(services.Single(line => line.Service == RemoteService.Tmdb).RightNow.LastCall).IsSome().And.IsEqualTo(Midnight.AddHours(9));
		await Assert.That(report.Previous.Services.Single().RightNow.LastCall).IsSome().And.IsEqualTo(Midnight.AddHours(-20));
	}

	[Test]
	public async Task ReadAsync_RightNow_NamesAPauseTheRequestsLeftAndADownload()
	{
		_capacity.Of(RemoteService.Plex).Returns(RemoteLoad.Idle with { BlockedUntil = Some.Of(Now.AddMinutes(16)) });
		_capacity.Of(RemoteService.Tmdb).Returns(RemoteLoad.Idle with { BlockedUntil = Some.Of(Now.AddMinutes(-1)) });
		_mdblist.TodayAsync(Any()).Returns(Some.Of(new QuotaToday(Midnight.AddDays(1), Some.Of(Allowance(962)))));
		_loads.FindAsync(DatasetNames.ImdbRatings, Any()).Returns(Some.Of(new DatasetLoad(DatasetNames.ImdbRatings, Midnight.AddHours(4.5), new None(), 2)));
		Stored(
			Calls(RemoteService.Plex, 11, UsageOutcome.Answered, 1),
			Calls(RemoteService.Tmdb, 11, UsageOutcome.Answered, 1),
			Calls(RemoteService.Mdblist, 11, UsageOutcome.Answered, 1),
			Calls(RemoteService.ImdbDatasets, 4, UsageOutcome.Answered, 1)
		);

		var services = (await Testee().ReadAsync(UsageRange.Day)).Current.Services.ToDictionary(line => line.Service, line => line.RightNow);

		await Assert.That(services[RemoteService.Plex].PausedUntil).IsSome().And.IsEqualTo(Now.AddMinutes(16));
		await Assert.That(services[RemoteService.Tmdb].PausedUntil).IsNone();
		await Assert.That(services[RemoteService.Mdblist].RequestsLeft).IsSome().And.IsEqualTo(962);
		await Assert.That(services[RemoteService.Tmdb].RequestsLeft).IsNone();
		await Assert.That(services[RemoteService.ImdbDatasets].DownloadedAt).IsSome().And.IsEqualTo(Midnight.AddHours(4.5));
	}

	[Test]
	public async Task ReadAsync_WithoutAnMdblistKey_ListsNoQuota()
	{
		Stored(Calls(RemoteService.Tmdb, 11, UsageOutcome.Answered, 1));

		var report = await Testee().ReadAsync(UsageRange.Day);

		await Assert.That(report.Quotas).IsEmpty();
	}

	[Test]
	public async Task ReadAsync_ForAKeyWithNoAnswerSinceTheReset_ListsItsQuotaWithoutADay()
	{
		_mdblist.TodayAsync(Any()).Returns(Some.Of(new QuotaToday(Midnight.AddDays(1), new None())));
		Stored(Calls(RemoteService.Tmdb, 11, UsageOutcome.Answered, 1));

		var quota = (await Testee().ReadAsync(UsageRange.Day)).Quotas.Single();

		await Assert.That(quota.Service).IsEqualTo(RemoteService.Mdblist);
		await Assert.That(quota.Day).IsNone();
	}

	[Test]
	public async Task ReadAsync_ForAnAnsweredKey_DrawsTheDayFromTheRequestsSentSinceTheReset()
	{
		_mdblist.TodayAsync(Any()).Returns(Some.Of(new QuotaToday(Midnight.AddDays(1), Some.Of(Allowance(900)))));
		Stored(
			[Calls(RemoteService.Mdblist, 10, UsageOutcome.Answered, 60), Calls(RemoteService.Mdblist, 11, UsageOutcome.Answered, 40)],
			[Attempts(RemoteService.Mdblist, 10, 60, 250), Attempts(RemoteService.Mdblist, 11, 40, 250), Attempts(RemoteService.Mdblist, -3, 500, 250)]
		);

		var quota = (await Testee().ReadAsync(UsageRange.Day)).Quotas.Single();

		var day = await Assert.That(quota.Day).IsSome();
		await Assert.That((day!.Points[0].Left, day.Points[11].Left, day.Points[^1].Left)).IsEqualTo((1000L, 940L, 900L));
	}

	[Test]
	public async Task ReadAsync_AWeekForAKeyWithNoAnswerYet_CountsEachQuotaDaysRequests()
	{
		_mdblist.TodayAsync(Any()).Returns(Some.Of(new QuotaToday(Midnight.AddDays(1), new None())));
		Stored([], [Attempts(RemoteService.Mdblist, 10, 60, 250), Attempts(RemoteService.Mdblist, -1, 40, 250), Attempts(RemoteService.Tmdb, 10, 9, 250)]);

		var quota = (await Testee().ReadAsync(UsageRange.Week)).Quotas.Single();

		await Assert.That(quota.Days.Select(day => day.Total)).IsEquivalentTo([0L, 0, 0, 0, 0, 40, 60], CollectionOrdering.Matching);
		await Assert.That(quota.Days[^1].Start).IsEqualTo(Midnight);
	}

	[Test]
	public async Task ReadAsync_FeedEditorCalls_ListsTheirFeedsAndFoldsTheUnknownOnes()
	{
		var trending = TestFeeds.Feed(Guid.NewGuid(), "Trending shows");
		var weekly = TestFeeds.Feed(Guid.NewGuid(), "Top movies of the week");
		_feeds.GetAsync(Any()).Returns(new FeedSnapshot([FeedOverview.Of(trending, new None(), 0), FeedOverview.Of(weekly, new None(), 0)]));
		Stored(
			Edited(RemoteService.Mdblist, Some.Of(weekly.Id), 26),
			Edited(RemoteService.Mdblist, Some.Of(trending.Id), 30),
			Edited(RemoteService.Tmdb, Some.Of(trending.Id), 11),
			Edited(RemoteService.Mdblist, Some.Of(FeedId.From(Guid.NewGuid())), 5),
			Edited(RemoteService.Mdblist, new None(), 7),
			Calls(RemoteService.Mdblist, 11, UsageOutcome.Answered, 3)
		);

		var report = await Testee().ReadAsync(UsageRange.Day);

		var editor = report.Current.Causes.Single(line => line.Cause == UsageCause.FeedEditor);
		await Assert
			.That(editor.Feeds.Select(feed => (feed.Feed.Map(name => name.Name).GetValueOr("unknown"), feed.Calls)))
			.IsEquivalentTo([("Trending shows", 41L), ("Top movies of the week", 26L), ("unknown", 12L)], CollectionOrdering.Matching);
		var mdblist = report.Current.Services.Single(line => line.Service == RemoteService.Mdblist).Causes.Single(line => line.Cause == UsageCause.FeedEditor);
		await Assert.That(mdblist.Feeds.Select(feed => feed.Calls)).IsEquivalentTo([30L, 26L, 12L], CollectionOrdering.Matching);
		await Assert.That(report.Current.Causes.Single(line => line.Cause == UsageCause.FeedRun).Feeds).IsEmpty();
	}

	[Test]
	public async Task ReadAsync_AsAMember_IsRefused()
	{
		await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Testee(UserRole.Member).ReadAsync(UsageRange.Day));
	}

	private void Stored(params UsageCalls[] calls) => Stored(calls, []);

	private void Stored(IReadOnlyList<UsageCalls> calls, IReadOnlyList<UsageAttempts> attempts)
	{
		_store.ReadAsync(Any(), Any(), Any()).Returns(new UsageRows(calls, attempts));
	}

	private static QuotaAllowance Allowance(long remaining)
	{
		return new QuotaAllowance(remaining, Midnight.AddDays(1), Some.Of(1000L), new None(), new None(), 50, 0.5);
	}

	private static UsageAttempts Attempts(RemoteService service, int hour, long attempts, int bucket)
	{
		return new(service, Midnight.AddHours(hour), UsageCause.FeedRun, new None(), attempts, new Dictionary<int, long> { [bucket] = attempts });
	}

	private static UsageCalls Edited(RemoteService service, Optional<FeedId> feed, long calls)
	{
		return new(service, Midnight.AddHours(11), UsageCause.FeedEditor, UsageOutcome.Answered, feed, calls);
	}

	private static UsageCalls Calls(RemoteService service, int hour, UsageOutcome outcome, long calls, UsageCause cause = UsageCause.FeedRun)
	{
		return new(service, Midnight.AddHours(hour), cause, outcome, new None(), calls);
	}

	private UsageReport Testee(UserRole role = UserRole.Admin)
	{
		var clock = IDateTimeProvider.Mock();
		clock.Now.Getter.Returns(Now);
		return new UsageReport(
			_store.Object,
			_lastCalls.Object,
			_capacity.Object,
			_loads.Object,
			_mdblist.Object,
			new UsageFeedLines(_feeds.Object),
			clock.Object,
			new TestCurrentUser { Role = role }
		);
	}
}
