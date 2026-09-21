using System.Collections.Concurrent;
using System.Diagnostics;
using Contracts;
using Contracts.Blacklist;
using Contracts.DI;
using Contracts.Feed;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Processing;
using Contracts.Watchlist;
using Core.Processing;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Processing;
using Domain.Providers;
using Domain.Watchlist;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Feed;

internal sealed class FeedRunEngine(IServiceScopeFactory scopeFactory, ILogger<FeedRunEngine> logger) : IFeedRunEngine
{
	private readonly ConcurrentDictionary<FeedId, SemaphoreSlim> _gates = new();

	public async Task<FeedRunSummary> DryRunAsync(
		Domain.Feed.Feed feed,
		IReadOnlyList<MediaLookup> items,
		FeedSource source,
		ICurrentUser user,
		CancellationToken cancellationToken = default
	)
	{
		await using var scope = scopeFactory.CreateAsyncScope();
		using var _ = scope.ServiceProvider.GetRequiredService<IUserScope>().As(user);
		return await PlanAsync(Resolve(scope), feed, items, source, cancellationToken);
	}

	public async Task<FeedRunResult> PreviewAsync(FeedId feedId, ICurrentUser user, CancellationToken cancellationToken = default)
	{
		try
		{
			await using var scope = scopeFactory.CreateAsyncScope();
			using var _ = scope.ServiceProvider.GetRequiredService<IUserScope>().As(user);
			var deps = Resolve(scope);

			if (await deps.Feeds.GetFeedByIdAsync(feedId, cancellationToken) is not { } feed)
			{
				return new FeedRunFailed("The feed no longer exists.");
			}

			var (items, source) = await FetchAsync(deps, feed, cancellationToken);
			return await PlanAsync(deps, feed, items, source, cancellationToken);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Feed preview {FeedId} failed", feedId);
			return new FeedRunFailed(ex.Message);
		}
	}

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

		var (items, source) = await FetchAsync(deps, feed, cancellationToken);
		var planned = await PlanAsync(deps, feed, items, source, cancellationToken);

		var run = new Domain.Feed.Run.FeedRun
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
		await deps.RunLog.StartAsync(run, cancellationToken);

		var added = await deps.Enrollment.AddToWatchlistAsync(planned.Delta.Adds, new FeedOrigin(feed, run), cancellationToken);
		var removed = await deps.Enrollment.RemoveOwnedAsync(planned.Delta.Drops, feed.Id, cancellationToken);
		WarnOnDrift(feed, planned.Delta, added, removed);

		var applied = planned with { Delta = new FeedDelta(added, planned.Delta.Keeps, removed) };
		await deps.RunLog.CompleteAsync(
			run with
			{
				Duration = elapsed.Elapsed,
				Conclusiveness = applied.Conclusiveness,
				Counts = FeedRunCounts.From(applied),
				Items = Record(applied.Delta),
			},
			cancellationToken
		);

		return applied;
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

	private async Task<(IReadOnlyList<MediaLookup> Items, FeedSource Source)> FetchAsync(
		EngineDeps deps,
		Domain.Feed.Feed feed,
		CancellationToken cancellationToken
	)
	{
		var items = new List<MediaLookup>();
		try
		{
			await foreach (var item in deps.Providers[feed.Provider].GetFeedItemsAsync(feed.Identifier, cancellationToken).WithCancellation(cancellationToken))
			{
				items.Add(item);
			}
			return (items, FeedSource.Complete);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogWarning(ex, "Feed {FeedId} returned a partial list; removals are suppressed for this run", feed.Id);
			return (items, FeedSource.Partial);
		}
	}

	private static async Task<FeedRunSummary> PlanAsync(
		EngineDeps deps,
		Domain.Feed.Feed feed,
		IReadOnlyList<MediaLookup> items,
		FeedSource source,
		CancellationToken cancellationToken
	)
	{
		var unresolved = items.Where(item => item is UnmatchedMedia).ToList();
		var resolved = items.Where(item => item is not UnmatchedMedia).Select(MediaQueryRow.From).ToList();

		var entries = await deps.Watchlist.GetEntriesAsync(cancellationToken);
		var blacklisted = await deps.Blacklist.GetBlacklistIdsAsync(cancellationToken);
		var watchlisted = entries.Select(entry => entry.MediaId).ToHashSet();
		var owned = entries.Where(entry => entry.Provenance is FeedOrigin origin && origin.Feed.Id == feed.Id).Select(entry => entry.MediaId).ToHashSet();

		var options = new QueryOptions
		{
			Sort = feed.Sort,
			Limit = feed.Limit,
			Filter = feed.Filter,
		};
		var predicates = new QueryPredicates<MediaQueryRow>(
			IsWatchlisted: row => watchlisted.Contains(row.Media.Identity.Id),
			IsBlacklisted: row => blacklisted.Contains(row.Media.Identity.Id),
			IsOwned: row => owned.Contains(row.Media.Identity.Id)
		);

		var classified =
			resolved.Count > 0 ? await deps.Processor.ExecuteAsync(resolved, options, predicates, cancellationToken).ToListAsync(cancellationToken) : [];
		var verdicts = classified
			.Select(verdict => new QueryVerdict<MediaLookup>(verdict.Item.Media, verdict.Outcome, verdict.Enrollment, verdict.ConsumesSlot))
			.ToList();

		var window = feed.Strategy is Mirror mirror ? mirror.AbsenceThreshold : 1;
		var absences = await deps.RunLog.GetAbsencesAsync(feed.Id, owned, window, cancellationToken);

		var plan = FeedRunPlan.Compute(verdicts, unresolved.Count, owned, absences, feed.Strategy, source);
		return new FeedRunSummary(verdicts, unresolved, plan.Delta, plan.Conclusiveness, plan.RunsRemaining);
	}

	private static EngineDeps Resolve(AsyncServiceScope scope) =>
		new(
			Watchlist: scope.ServiceProvider.GetRequiredService<IWatchlistRepository>(),
			Enrollment: scope.ServiceProvider.GetRequiredService<IWatchlistService>(),
			Blacklist: scope.ServiceProvider.GetRequiredService<IBlacklistService>(),
			RunLog: scope.ServiceProvider.GetRequiredService<IFeedRunLog>(),
			Processor: scope.ServiceProvider.GetRequiredService<QueryProcessor<MediaQueryRow>>(),
			Feeds: scope.ServiceProvider.GetRequiredService<IFeedRepository>(),
			Providers: scope.ServiceProvider.GetRequiredService<IKeyedServiceIndex<FeedProvider, IFeedService>>(),
			Clock: scope.ServiceProvider.GetRequiredService<IDateTimeProvider>()
		);

	private readonly record struct EngineDeps(
		IWatchlistRepository Watchlist,
		IWatchlistService Enrollment,
		IBlacklistService Blacklist,
		IFeedRunLog RunLog,
		QueryProcessor<MediaQueryRow> Processor,
		IFeedRepository Feeds,
		IKeyedServiceIndex<FeedProvider, IFeedService> Providers,
		IDateTimeProvider Clock
	);
}
