using System.Diagnostics;
using Contracts;
using Contracts.Feed;
using Contracts.Identity;
using Contracts.Usage;
using Contracts.Wantlist;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Wantlist;
using Microsoft.Extensions.Logging;

namespace Core.Feed;

internal sealed class FeedRunEngine(
	FeedRunGate gate,
	FeedRunPlanner planner,
	IWantlistService wantlist,
	IFeedRepository feeds,
	IFeedRunUsage usage,
	IFeedRunTrace trace,
	ICurrentUser currentUser,
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
		var result = await GuardedAsync(feedId, trigger, cancellationToken);
		trace.End(feedId, result);
		return result;
	}

	private async Task<FeedRunResult> GuardedAsync(FeedId feedId, FeedRunTrigger trigger, CancellationToken cancellationToken)
	{
		try
		{
			return await feeds
				.FindAsync(feedId, cancellationToken)
				.Map(async feed => await ExecuteAsync(feed, trigger, cancellationToken))
				.GetValueOr(new FeedRunFailed("The feed no longer exists."));
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Feed run {FeedId} failed", feedId);
			return new FeedRunFailed(ex.Message);
		}
	}

	private async Task<FeedRunResult> ExecuteAsync(Domain.Feed.Feed feed, FeedRunTrigger trigger, CancellationToken cancellationToken)
	{
		usage.Start(feed.Id);
		using var run = trace.Start(feed.Id, currentUser.Id);
		try
		{
			return await RecordAsync(feed, trigger, cancellationToken);
		}
		finally
		{
			usage.End(feed.Id);
		}
	}

	private async Task<FeedRunResult> RecordAsync(Domain.Feed.Feed feed, FeedRunTrigger trigger, CancellationToken cancellationToken)
	{
		var startedAt = clock.Now;
		var elapsed = Stopwatch.StartNew();

		var planned = await planner.PlanAsync(feed, cancellationToken);

		var run = new FeedRun
		{
			Id = RunId.From(Guid.NewGuid()),
			FeedId = feed.Id,
			StartedAt = startedAt,
			Duration = elapsed.Elapsed,
			Strategy = feed.Strategy,
			Trigger = trigger,
			Conclusiveness = planned.Conclusiveness,
			Counts = FeedRunCounts.From(planned),
			Items = Record(planned.Delta),
			SourceFailure = planned.SourceFailure,
		};
		await feeds.StartRunAsync(run, cancellationToken);

		var added = await wantlist.AddAsync(planned.Delta.Adds, new FeedOrigin(feed, run), cancellationToken);
		var removed = await wantlist.RemoveAsync(planned.Delta.Drops, addedBy: feed.Id, cancellationToken);
		WarnOnDrift(feed, planned.Delta, added, removed);

		var applied = planned.Delta with { Adds = added, Drops = removed };
		var cost = usage.End(feed.Id);
		var completed = run with
		{
			Duration = elapsed.Elapsed,
			Conclusiveness = planned.Conclusiveness,
			Counts = FeedRunCounts.From(planned, applied),
			Items = Record(applied),
			Calls = cost.Calls,
			Times = cost.Times,
		};
		await feeds.CompleteRunAsync(completed, cancellationToken);

		return completed.Failed
			? new FeedRunFailed(completed.SourceFailure.GetValueOr("The source returned nothing."))
			: new FeedRunCompleted(applied, planned.Conclusiveness);
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
