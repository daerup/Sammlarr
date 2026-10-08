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
		_capacity.Of(Any()).Returns(new RemoteLoad(new None(), 0));
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
		_capacity.Of(RemoteService.Plex).Returns(new RemoteLoad(Some.Of(Now.AddMinutes(16)), 0));
		_capacity.Of(RemoteService.Tmdb).Returns(new RemoteLoad(Some.Of(Now.AddMinutes(-1)), 0));
		_mdblist.TodayAsync(Any()).Returns(Some.Of(962L));
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
	public async Task ReadAsync_AsAMember_IsRefused()
	{
		await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Testee(UserRole.Member).ReadAsync(UsageRange.Day));
	}

	private void Stored(params UsageCalls[] calls)
	{
		_store.ReadAsync(Any(), Any(), Any()).Returns(new UsageRows(calls, []));
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
