using Contracts.Feed;
using Contracts.Usage;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Usage;

namespace Core.Usage;

internal sealed record UsageFeeds(IReadOnlyList<UsageFeedLine> Lines, IReadOnlyDictionary<FeedId, UsageFeedName> Names);

internal sealed class UsageFeedLines(IFeedService feeds, IDeletedFeeds deleted, IUsageStore store)
{
	private const int MostRuns = 10_000;

	public async Task<UsageFeeds> ReadAsync(UsageWindow window, UsageRows rows, CancellationToken cancellationToken)
	{
		var snapshot = await feeds.GetAsync(cancellationToken);
		var runs = await feeds.GetRunsAsync(RunQuery.Take(MostRuns) with { Since = Some.Of(window.From), WithItems = false }, cancellationToken);
		var gone = await deleted.ReadAsync(cancellationToken);
		var runsByFeed = runs.Where(run => window.Contains(run.StartedAt)).ToLookup(run => run.FeedId);
		var callsByFeed = rows.Calls.Where(row => row.Cause == UsageCause.FeedRun && window.Contains(row.Hour)).ToLookup(row => row.Feed.ToNullable());
		IReadOnlyList<UsageFeedLine> live =
		[
			.. snapshot
				.Feeds.Select(overview =>
					Line(overview.Feed, overview.LastRun.Map(last => last.StartedAt), [.. runsByFeed[overview.Feed.Id].OrderBy(run => run.StartedAt).Select(Run)])
				)
				.OrderByDescending(line => line.Stats.Calls),
		];
		IEnumerable<UsageFeedLine> ended = gone.Where(feed => callsByFeed.Contains(feed.Feed))
			.Select(feed => Ended(feed, [.. callsByFeed[feed.Feed]]))
			.OrderByDescending(line => line.Stats.Calls);
		var names = await store.FeedNamesAsync(cancellationToken);
		return new UsageFeeds([.. live, .. ended], names.DistinctBy(name => name.Feed).ToDictionary(name => name.Feed));
	}

	private static UsageFeedLine Line(Domain.Feed.Feed feed, Optional<DateTimeOffset> lastRun, IReadOnlyList<UsageRun> runs)
	{
		var services = runs.SelectMany(run => run.Services.Keys).Distinct();
		return new UsageFeedLine(
			feed.Id,
			feed.Name,
			feed.Provider,
			feed.Enabled,
			lastRun,
			runs,
			Stats(runs),
			services.ToDictionary(
				service => service,
				service => Stats([.. runs.Where(run => run.Services.ContainsKey(service)).Select(run => run with { Calls = run.Services[service] })])
			)
		);
	}

	private static UsageFeedLine Ended(DeletedFeed feed, IReadOnlyList<UsageCalls> calls)
	{
		var byService = calls.GroupBy(row => row.Service).ToDictionary(service => service.Key, service => Outcomes(service));
		return new UsageFeedLine(
			feed.Feed,
			feed.Name,
			feed.Provider,
			false,
			new None(),
			[],
			Called(calls.Sum(row => row.Calls)),
			byService.ToDictionary(service => service.Key, service => Called(service.Value.Total))
		)
		{
			Gone = Some.Of(new UsageFeedGone(feed.DeletedAt, byService)),
		};
	}

	private static UsageFeedStats Called(long calls) => new(calls, 0, 0, 0, 0, 0, TimeSpan.Zero);

	private static UsageRun Run(FeedRun run)
	{
		var calls = Tally(run.Calls);
		return new UsageRun(
			run.Id,
			run.StartedAt,
			run.Duration,
			run.Trigger,
			run.Counts.Total,
			run.Counts.Added,
			run.Counts.Removed,
			run.Counts.UnmatchedNotFound + run.Counts.UnmatchedConflict,
			run.Counts.UnmatchedCouldNotLookUp,
			calls,
			run.Calls.GroupBy(call => call.Service).ToDictionary(group => group.Key, Tally)
		);
	}

	private static UsageTally Tally(IEnumerable<FeedRunCalls> calls)
	{
		return new UsageTally(
			calls.GroupBy(call => call.Outcome).ToDictionary(group => group.Key, group => group.Sum(call => call.Calls)),
			new Dictionary<int, long>()
		);
	}

	private static UsageTally Outcomes(IEnumerable<UsageCalls> calls)
	{
		return new UsageTally(
			calls.GroupBy(call => call.Outcome).ToDictionary(group => group.Key, group => group.Sum(call => call.Calls)),
			new Dictionary<int, long>()
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
