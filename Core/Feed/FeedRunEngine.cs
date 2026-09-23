using System.Diagnostics;
using Contracts;
using Contracts.Feed;
using Contracts.Watchlist;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Watchlist;
using Microsoft.Extensions.Logging;

namespace Core.Feed;

internal sealed class FeedRunEngine(
	FeedRunGate gate,
	FeedRunPlanner planner,
	IWatchlistService watchlist,
	IFeedRepository feeds,
	IDateTimeProvider clock,
	ILogger<FeedRunEngine> logger
) : IFeedRunEngine
{
	public async Task<FeedRunResult> RunAsync(FeedId feedId, FeedRunTrigger trigger, CancellationToken cancellationToken = default)
	{
		if (await gate.EnterAsync(feedId, cancellationToken) is not Some<IDisposable>(var lease))
		{
			return new FeedRunAlreadyRunning();
		}

		using var _ = lease;
		try
		{
			if (await feeds.GetFeedByIdAsync(feedId, cancellationToken) is not { } feed)
			{
				return new FeedRunFailed("The feed no longer exists.");
			}

			return await ExecuteAsync(feed, trigger, cancellationToken);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Feed run {FeedId} failed", feedId);
			return new FeedRunFailed(ex.Message);
		}
	}

	private async Task<FeedRunResult> ExecuteAsync(Domain.Feed.Feed feed, FeedRunTrigger trigger, CancellationToken cancellationToken)
	{
		var startedAt = clock.Now;
		var elapsed = Stopwatch.StartNew();

		var planned = await planner.PlanAsync(feed, cancellationToken);

		var run = new FeedRun
		{
			Id = Guid.NewGuid(),
			FeedId = feed.Id,
			StartedAt = startedAt,
			Duration = elapsed.Elapsed,
			Strategy = feed.Strategy,
			Trigger = trigger,
			Conclusiveness = planned.Conclusiveness,
			Counts = FeedRunCounts.From(planned),
			Items = Record(planned.Delta),
		};
		await feeds.StartRunAsync(run, cancellationToken);

		var added = await watchlist.AddToWatchlistAsync(planned.Delta.Adds, new FeedOrigin(feed, run), cancellationToken);
		var removed = await watchlist.RemoveOwnedAsync(planned.Delta.Drops, feed.Id, cancellationToken);
		WarnOnDrift(feed, planned.Delta, added, removed);

		var applied = planned.Delta with { Adds = added, Drops = removed };
		await feeds.CompleteRunAsync(
			run with
			{
				Duration = elapsed.Elapsed,
				Conclusiveness = planned.Conclusiveness,
				Counts = FeedRunCounts.From(planned, applied),
				Items = Record(applied),
			},
			cancellationToken
		);

		return new FeedRunCompleted(applied, planned.Conclusiveness);
	}

	private void WarnOnDrift(Domain.Feed.Feed feed, FeedDelta planned, IReadOnlyList<MediaId> added, IReadOnlyList<MediaId> removed)
	{
		if (planned.Adds.Count == added.Count && planned.Drops.Count == removed.Count)
		{
			return;
		}

		logger.LogWarning(
			"Feed run {FeedId} planned {PlannedAdds} adds and {PlannedDrops} removals but applied {ActualAdds} and {ActualRemovals}",
			feed.Id,
			planned.Adds.Count,
			planned.Drops.Count,
			added.Count,
			removed.Count
		);
	}

	private static IReadOnlyList<FeedRunItem> Record(FeedDelta delta) =>
		[
			.. delta.Adds.Select(id => new FeedRunItem { MediaId = id, State = FeedRunItemState.Added }),
			.. delta.Keeps.Select(id => new FeedRunItem { MediaId = id, State = FeedRunItemState.Kept }),
			.. delta.Drops.Select(id => new FeedRunItem { MediaId = id, State = FeedRunItemState.Removed }),
		];
}
