using Contracts.Feed;
using Contracts.Usage;
using Core.Usage;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Providers;
using Domain.Usage;
using TestInfrastructure;
using TUnit.Assertions.Enums;

namespace Core.Tests.Usage;

public class UsageFeedLinesTests
{
	private static readonly DateTimeOffset To = new(2026, 10, 7, 13, 0, 0, TimeSpan.Zero);

	private static readonly UsageWindow Day = new(To.AddHours(-24), To, TimeSpan.FromHours(1));

	private static readonly Domain.Feed.Feed Busy = TestFeeds.Feed(Guid.NewGuid(), "Latest shows");

	private static readonly Domain.Feed.Feed Idle = TestFeeds.Feed(Guid.NewGuid(), "Paused list") with { Enabled = false };

	private static readonly UsageRows NoRows = new([], []);

	private readonly Mock<IFeedService> _feeds = IFeedService.Mock();

	private readonly Mock<IDeletedFeeds> _deleted = IDeletedFeeds.Mock();

	public UsageFeedLinesTests()
	{
		_deleted.ReadAsync(Any()).Returns([]);
	}

	[Test]
	public async Task ReadAsync_ForAFeedsRunsInTheWindow_GivesTheTypicalRun()
	{
		Stored(Run(-20, 10, 1, 0, 1), Run(-10, 1000, 2, 1, 20), Run(-2, 30, 3, 0, 2), Run(-30, 5000, 50, 50, 60));

		var busy = (await Testee().ReadAsync(Day, NoRows, CancellationToken.None)).Lines.First();

		await Assert.That(busy.Runs).Count().IsEqualTo(3);
		await Assert.That(busy.Stats).IsEqualTo(new UsageFeedStats(1040, 30, 0.3, 3, 0.03, 1, TimeSpan.FromSeconds(2)));
	}

	[Test]
	public async Task ReadAsync_ARun_CarriesHowItWasStartedAndWhatItChanged()
	{
		Stored(Run(-2, 30, 3, 0, 2) with { Trigger = new Scheduled("0 0 3 * * *"), Counts = FeedRunCounts.None with { Total = 100, Added = 4, Removed = 1 } });

		var run = (await Testee().ReadAsync(Day, NoRows, CancellationToken.None)).Lines.First().Runs.Single();

		await Assert.That(run.Trigger).IsOfUnionType<FeedRunTrigger, Scheduled>();
		await Assert.That((run.Added, run.Removed)).IsEqualTo((4, 1));
	}

	[Test]
	public async Task ReadAsync_ARunAcrossServices_CountsEachServicesCallsAndTheirStats()
	{
		Stored(
			Run(-3, 0, 1, 0, 1) with
			{
				Calls =
				[
					new FeedRunCalls(RemoteService.Tmdb, UsageOutcome.Answered, 10),
					new FeedRunCalls(RemoteService.Plex, UsageOutcome.Answered, 4),
					new FeedRunCalls(RemoteService.Plex, UsageOutcome.Failed, 1),
				],
			},
			Run(-2, 30, 3, 0, 2)
		);

		var busy = (await Testee().ReadAsync(Day, NoRows, CancellationToken.None)).Lines.First();

		var mixed = busy.Runs.Single(run => run.Services.ContainsKey(RemoteService.Plex));
		await Assert.That((mixed.Services[RemoteService.Tmdb].Total, mixed.Services[RemoteService.Plex].Total)).IsEqualTo((10L, 5L));
		await Assert.That((busy.Services[RemoteService.Tmdb].Calls, busy.Services[RemoteService.Plex].Calls)).IsEqualTo((40L, 5L));
		await Assert.That(busy.Services[RemoteService.Plex]).IsEqualTo(new UsageFeedStats(5, 2.5, 0.025, 2, 0.02, 0, TimeSpan.FromSeconds(1.5)));
	}

	[Test]
	public async Task ReadAsync_ForEveryFeed_ListsTheBusiestFirstAndAFeedThatNeverRan()
	{
		Stored(Run(-2, 30, 3, 0, 2));

		var lines = (await Testee().ReadAsync(Day, NoRows, CancellationToken.None)).Lines;

		await Assert
			.That(lines.Select(line => (line.Name, line.Enabled, line.LastRun.Map(_ => true).GetValueOr(false))))
			.IsEquivalentTo([("Latest shows", true, true), ("Paused list", false, false)], CollectionOrdering.Matching);
		await Assert.That(lines.Last().Stats.Calls).IsEqualTo(0);
	}

	[Test]
	public async Task ReadAsync_ADeletedFeedThatRanInTheWindow_KeepsItsNameAndItsCallsPerService()
	{
		Stored(Run(-2, 30, 3, 0, 2));
		var gone = new DeletedFeed(FeedId.From(Guid.NewGuid()), "Horror movies", FeedProvider.Mdblist, To.AddHours(-5));
		_deleted.ReadAsync(Any()).Returns([gone]);
		UsageRows rows = new(
			[
				new UsageCalls(RemoteService.Mdblist, To.AddHours(-10), UsageCause.FeedRun, UsageOutcome.Answered, Some.Of(gone.Feed), 50),
				new UsageCalls(RemoteService.Mdblist, To.AddHours(-10), UsageCause.FeedRun, UsageOutcome.AnsweredAfter429, Some.Of(gone.Feed), 3),
				new UsageCalls(RemoteService.Tmdb, To.AddHours(-9), UsageCause.FeedRun, UsageOutcome.Answered, Some.Of(gone.Feed), 5),
				new UsageCalls(RemoteService.Tmdb, To.AddHours(-30), UsageCause.FeedRun, UsageOutcome.Answered, Some.Of(gone.Feed), 99),
				new UsageCalls(RemoteService.Mdblist, To.AddHours(-9), UsageCause.FeedEditor, UsageOutcome.Answered, Some.Of(gone.Feed), 7),
			],
			[]
		);

		var feeds = await Testee().ReadAsync(Day, rows, CancellationToken.None);

		var line = feeds.Lines.Last();
		await Assert.That(line.Name).IsEqualTo("Horror movies");
		await Assert.That(line.Stats.Calls).IsEqualTo(58);
		await Assert.That(line.Runs).IsEmpty();
		var ended = await Assert.That(line.Gone).IsSome();
		await Assert.That(ended!.DeletedAt).IsEqualTo(To.AddHours(-5));
		await Assert.That((ended.Calls[RemoteService.Mdblist].Total, ended.Calls[RemoteService.Tmdb].Total)).IsEqualTo((53L, 5L));
		await Assert.That(line.Services[RemoteService.Mdblist].Calls).IsEqualTo(53);
		await Assert.That(feeds.Names[gone.Feed].DeletedAt).IsSome().And.IsEqualTo(To.AddHours(-5));
	}

	[Test]
	public async Task ReadAsync_ADeletedFeedWithoutRunsInTheWindow_HasNoLineButKeepsItsName()
	{
		Stored(Run(-2, 30, 3, 0, 2));
		var gone = new DeletedFeed(FeedId.From(Guid.NewGuid()), "Horror movies", FeedProvider.Mdblist, To.AddHours(-5));
		_deleted.ReadAsync(Any()).Returns([gone]);

		var feeds = await Testee().ReadAsync(Day, NoRows, CancellationToken.None);

		await Assert.That(feeds.Lines.Select(line => line.Name)).IsEquivalentTo(["Latest shows", "Paused list"]);
		await Assert.That(feeds.Names[gone.Feed].Name).IsEqualTo("Horror movies");
		await Assert.That(feeds.Names[Busy.Id].DeletedAt).IsNone();
	}

	private void Stored(params FeedRun[] runs)
	{
		_feeds
			.GetAsync(Any())
			.Returns(
				new FeedSnapshot([new FeedOverview(Busy, Some.Of(runs[^1]), FeedRunHistory.None, 0), new FeedOverview(Idle, new None(), FeedRunHistory.None, 0)])
			);
		_feeds.GetRunsAsync(Any(), Any()).Returns(runs);
	}

	private static FeedRun Run(int hours, long calls, int notFound, int couldNotLookUp, int seconds)
	{
		return TestFeeds.Run(Busy, Guid.NewGuid()) with
		{
			StartedAt = To.AddHours(hours),
			Duration = TimeSpan.FromSeconds(seconds),
			Counts = FeedRunCounts.None with { Total = 100, UnmatchedNotFound = notFound, UnmatchedCouldNotLookUp = couldNotLookUp },
			Calls = [new FeedRunCalls(RemoteService.Tmdb, UsageOutcome.Answered, calls)],
		};
	}

	private UsageFeedLines Testee() => new(_feeds.Object, _deleted.Object);
}
