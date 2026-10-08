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

	private readonly Mock<IFeedService> _feeds = IFeedService.Mock();

	[Test]
	public async Task ReadAsync_ForAFeedsRunsInTheWindow_GivesTheTypicalRun()
	{
		Stored(Run(-20, 10, 1, 0, 1), Run(-10, 1000, 2, 1, 20), Run(-2, 30, 3, 0, 2), Run(-30, 5000, 50, 50, 60));

		var busy = (await Testee().ReadAsync(Day, CancellationToken.None)).First();

		await Assert.That(busy.Runs).Count().IsEqualTo(3);
		await Assert.That(busy.Stats).IsEqualTo(new UsageFeedStats(1040, 30, 0.3, 3, 0.03, 1, TimeSpan.FromSeconds(2)));
	}

	[Test]
	public async Task ReadAsync_ARun_CarriesHowItWasStartedAndWhatItChanged()
	{
		Stored(Run(-2, 30, 3, 0, 2) with { Trigger = new Scheduled("0 0 3 * * *"), Counts = FeedRunCounts.None with { Total = 100, Added = 4, Removed = 1 } });

		var run = (await Testee().ReadAsync(Day, CancellationToken.None)).First().Runs.Single();

		await Assert.That(run.Trigger).IsOfUnionType<FeedRunTrigger, Scheduled>();
		await Assert.That((run.Added, run.Removed)).IsEqualTo((4, 1));
	}

	[Test]
	public async Task ReadAsync_ForEveryFeed_ListsTheBusiestFirstAndAFeedThatNeverRan()
	{
		Stored(Run(-2, 30, 3, 0, 2));

		var lines = await Testee().ReadAsync(Day, CancellationToken.None);

		await Assert
			.That(lines.Select(line => (line.Name, line.Enabled, line.LastRun.Map(_ => true).GetValueOr(false))))
			.IsEquivalentTo([("Latest shows", true, true), ("Paused list", false, false)], CollectionOrdering.Matching);
		await Assert.That(lines.Last().Stats.Calls).IsEqualTo(0);
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

	private UsageFeedLines Testee() => new(_feeds.Object);
}
