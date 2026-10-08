using Contracts.Feed;
using Contracts.Usage;
using Domain.Feed.Run;
using Domain.Usage;

namespace Core.Usage;

internal sealed class UsageFeedLines(IFeedService feeds)
{
	private const int MostRuns = 10_000;

	public async Task<IReadOnlyList<UsageFeedLine>> ReadAsync(UsageWindow window, CancellationToken cancellationToken)
	{
		var snapshot = await feeds.GetAsync(cancellationToken);
		var runs = await feeds.GetRunsAsync(RunQuery.Take(MostRuns) with { Since = Some.Of(window.From), WithItems = false }, cancellationToken);
		var runsByFeed = runs.Where(run => window.Contains(run.StartedAt)).ToLookup(run => run.FeedId);
		return
		[
			.. snapshot
				.Feeds.Select(overview =>
					Line(overview.Feed, overview.LastRun.Map(last => last.StartedAt), [.. runsByFeed[overview.Feed.Id].OrderBy(run => run.StartedAt).Select(Run)])
				)
				.OrderByDescending(line => line.Stats.Calls),
		];
	}

	private static UsageFeedLine Line(Domain.Feed.Feed feed, Optional<DateTimeOffset> lastRun, IReadOnlyList<UsageRun> runs)
	{
		return new UsageFeedLine(feed.Id, feed.Name, feed.Provider, feed.Enabled, lastRun, runs, Stats(runs));
	}

	private static UsageRun Run(FeedRun run)
	{
		var calls = run.Calls.GroupBy(call => call.Outcome).ToDictionary(group => group.Key, group => group.Sum(call => call.Calls));
		return new UsageRun(
			run.Id,
			run.StartedAt,
			run.Duration,
			run.Counts.Total,
			run.Counts.UnmatchedNotFound,
			run.Counts.UnmatchedCouldNotLookUp,
			new UsageTally(calls, new Dictionary<int, long>())
		);
	}

	private static UsageFeedStats Stats(IReadOnlyList<UsageRun> runs)
	{
		var titled = runs.Where(run => run.Titles > 0).ToList();
		return new UsageFeedStats(
			runs.Sum(run => run.Calls.Total),
			Medians.Of(runs.Select(run => (double)run.Calls.Total)),
			Medians.Of(titled.Select(run => (double)run.Calls.Total / run.Titles)),
			Medians.Of(runs.Select(run => (double)(run.NotFound + run.CouldNotLookUp))),
			Medians.Of(titled.Select(run => (double)(run.NotFound + run.CouldNotLookUp) / run.Titles)),
			runs.Sum(run => run.CouldNotLookUp),
			TimeSpan.FromMilliseconds(Medians.Of(runs.Select(run => run.Took.TotalMilliseconds)))
		);
	}
}
