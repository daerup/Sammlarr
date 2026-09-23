using Contracts.DI;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Contracts.Watchlist.Sync;
using Domain.Media;
using Domain.Providers;
using Domain.Watchlist.Sync;
using Microsoft.Extensions.Logging;

namespace Core.Watchlist.Sync;

internal sealed class WatchlistSyncPlanner(
	IProviderConfigRepository config,
	IWatchlistRepository repo,
	IWatchlistOutbox outbox,
	IMediaService media,
	IKeyedServiceIndex<WatchlistProvider, IWatchlistProvider> remotes,
	ILogger<WatchlistSyncPlanner> logger
) : IWatchlistSyncPlanner
{
	public async Task<WatchlistPlanResult> PlanAsync(CancellationToken cancellationToken = default)
	{
		var target = await TargetAsync(cancellationToken);
		return await PlanAsync(target, cancellationToken) switch
		{
			Error error => OnPlanError(error, target.Provider),
			Success<WatchlistSyncPlan>(var plan) => Project(plan),
		};
	}

	private async Task<SyncTarget> TargetAsync(CancellationToken cancellationToken)
	{
		var provider = (await config.GetAsync(cancellationToken)).WatchlistProvider;
		return new SyncTarget(provider, provider.AsMediaProvider(), remotes[provider]);
	}

	private async Task<Result<WatchlistSyncPlan>> PlanAsync(SyncTarget target, CancellationToken cancellationToken) =>
		await FetchRemoteAsync(target, cancellationToken)
			.Map(async remote =>
			{
				var localIds = await repo.GetIdsAsync(cancellationToken);
				var outboxOps = await outbox.GetAsync(cancellationToken);
				var plan = WatchlistSyncPlan.Compute(remote.Resolved, localIds, outboxOps) with { Unmatched = remote.Unmatched };
				await DropRedundantAsync(target, plan, cancellationToken);
				return Success.Of(plan);
			});

	private async Task<Result<RemoteWatchlist>> FetchRemoteAsync(SyncTarget target, CancellationToken cancellationToken)
	{
		try
		{
			var entries = await target.Remote.GetWatchlistAsync(cancellationToken).ToListAsync(cancellationToken);
			var requests = entries.Select(entry => new ResolveRequest([.. entry.Externals], target.MediaProvider, new None()));
			var remoteMediaIds = new List<MediaId>(entries.Count);
			var unmatched = new List<UnmatchedMedia>();
			var pipeline = media.IdentifyAsync(requests, cancellationToken).ZipAwait(entries, cancellationToken);
			await foreach (var (result, entry) in pipeline)
			{
				if (result is Success<MediaIdentity>(var identity))
				{
					remoteMediaIds.Add(identity.Id);
				}
				else
				{
					unmatched.Add(entry);
					logger.LogWarning("Remote watchlist item {Title} could not be resolved during sync", entry.Title ?? entry.Externals.FirstOrDefault()?.Value);
				}
			}
			return Success.Of(new RemoteWatchlist(remoteMediaIds, unmatched));
		}
		catch (Exception ex)
		{
			return Error.From(ex);
		}
	}

	private async Task DropRedundantAsync(SyncTarget target, WatchlistSyncPlan plan, CancellationToken cancellationToken)
	{
		if (plan.Redundant.Count == 0)
		{
			return;
		}

		await outbox.DropAsync(plan.Redundant, cancellationToken);
		logger.LogInformation("Dropped {Count} watchlist outbox entries {Provider} already agrees with", plan.Redundant.Count, target.Provider);
	}

	private WatchlistSyncRemoteFailed OnPlanError(Error error, WatchlistProvider provider)
	{
		logger.LogWarning("Remote fetch failed for {Provider}: {Reason}", provider, error.Reason);
		return new WatchlistSyncRemoteFailed(error.Reason);
	}

	private static WatchlistSyncSummary Project(WatchlistSyncPlan plan) =>
		new WatchlistSyncSummary(plan.Pull, plan.Push, [.. plan.NoOps, .. plan.Redundant]) { Unmatched = plan.Unmatched };

	private readonly record struct RemoteWatchlist(IReadOnlyList<MediaId> Resolved, IReadOnlyList<UnmatchedMedia> Unmatched);

	private readonly record struct SyncTarget(WatchlistProvider Provider, MediaProvider MediaProvider, IWatchlistProvider Remote);
}
