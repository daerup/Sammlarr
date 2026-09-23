using System.Collections.Concurrent;
using System.Diagnostics;
using Contracts;
using Contracts.Feed;
using Contracts.Identity;
using Contracts.Watchlist;
using Domain.Feed;
using Domain.Feed.Planning;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Watchlist;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Feed;

internal sealed class FeedRunEngine(IServiceScopeFactory scopeFactory, ILogger<FeedRunEngine> logger) : IFeedRunEngine
{
	private readonly ConcurrentDictionary<FeedId, SemaphoreSlim> _gates = new();

	public async Task<FeedRunResult> RunAsync(FeedId feedId, FeedRunTrigger trigger, ICurrentUser user, CancellationToken cancellationToken = default)
	{
		var gate = _gates.GetOrAdd(feedId, _ => new SemaphoreSlim(1, 1));
		if (!await gate.WaitAsync(0, cancellationToken))
		{
			return new FeedRunAlreadyRunning();
		}

		try
		{
			await using var scope = scopeFactory.CreateAsyncScope();
			using var _ = scope.ServiceProvider.GetRequiredService<IUserScope>().As(user);
			var deps = Resolve(scope);

			if (await deps.Feeds.GetFeedByIdAsync(feedId, cancellationToken) is not { } feed)
			{
				return new FeedRunFailed("The feed no longer exists.");
			}

			return await ExecuteAsync(deps, feed, trigger, cancellationToken);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Feed run {FeedId} failed", feedId);
			return new FeedRunFailed(ex.Message);
		}
		finally
		{
			gate.Release();
		}
	}

	private async Task<FeedRunResult> ExecuteAsync(EngineDeps deps, Domain.Feed.Feed feed, FeedRunTrigger trigger, CancellationToken cancellationToken)
	{
		var startedAt = deps.Clock.Now;
		var elapsed = Stopwatch.StartNew();

		var planned = await deps.Planner.PlanAsync(feed, cancellationToken);

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
		await deps.Feeds.StartRunAsync(run, cancellationToken);

		var added = await deps.Enrollment.AddToWatchlistAsync(planned.Delta.Adds, new FeedOrigin(feed, run), cancellationToken);
		var removed = await deps.Enrollment.RemoveOwnedAsync(planned.Delta.Drops, feed.Id, cancellationToken);
		WarnOnDrift(feed, planned.Delta, added, removed);

		var applied = new FeedDelta(added, planned.Delta.Keeps, removed);
		await deps.Feeds.CompleteRunAsync(
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

	private static EngineDeps Resolve(AsyncServiceScope scope) =>
		new(
			Planner: scope.ServiceProvider.GetRequiredService<FeedRunPlanner>(),
			Enrollment: scope.ServiceProvider.GetRequiredService<IWatchlistService>(),
			Feeds: scope.ServiceProvider.GetRequiredService<IFeedRepository>(),
			Clock: scope.ServiceProvider.GetRequiredService<IDateTimeProvider>()
		);

	private readonly record struct EngineDeps(FeedRunPlanner Planner, IWatchlistService Enrollment, IFeedRepository Feeds, IDateTimeProvider Clock);
}
