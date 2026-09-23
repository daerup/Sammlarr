using System.Diagnostics;
using Contracts;
using Contracts.DI;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Messaging;
using Contracts.Settings;
using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Contracts.Watchlist.Sync;
using Domain.Identity;
using Domain.Media;
using Domain.Notifications.Events;
using Domain.Providers;
using Domain.Watchlist;
using Domain.Watchlist.Outbox;
using Domain.Watchlist.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Observability.Tracing;

namespace Core.Watchlist.Sync;

internal sealed class WatchlistSyncEngine(
	WatchlistSyncGate gate,
	IProviderConfigRepository config,
	IWatchlistRepository repo,
	IWatchlistOutbox outbox,
	IMediaService media,
	IKeyedServiceIndex<WatchlistProvider, IWatchlistProvider> remotes,
	IMessagePublisher publisher,
	IDateTimeProvider clock,
	ICurrentUser currentUser,
	ILogger<WatchlistSyncEngine> logger
) : IWatchlistSyncEngine
{
	public async Task<WatchlistSyncResult> SyncAsync(WatchlistSyncTrigger trigger, CancellationToken cancellationToken = default)
	{
		if (await gate.EnterAsync(cancellationToken) is not Some<IDisposable>(var lease))
		{
			return new WatchlistSyncAlreadyRunning();
		}

		using var _ = lease;
		using var activity = Instrumentation.Source.StartActivity(nameof(WatchlistSyncEngine));
		activity?.SetTag("sammlarr.sync.trigger", trigger.ToString());

		var target = await TargetAsync(cancellationToken);
		activity?.SetTag("sammlarr.sync.provider", target.Provider.ToString());

		var planning = await PlanAsync(target, cancellationToken);
		WatchlistSyncResult syncResult = planning switch
		{
			Error error => OnPlanError(error, target.Provider),
			Success<WatchlistSyncPlan>(var plan) => await ExecuteAsync(plan, target, cancellationToken),
		};
		if (syncResult is WatchlistSyncRemoteFailed remoteFailed)
		{
			activity?.SetStatus(ActivityStatusCode.Error, remoteFailed.Reason);
			await publisher.PublishAsync(new WatchlistSyncFailedEvent(currentUser.Id, clock.Now, remoteFailed.Reason), cancellationToken);
		}
		return syncResult;
	}

	public async Task<WatchlistDryRunResult> DryRunAsync(CancellationToken cancellationToken = default)
	{
		var target = await TargetAsync(cancellationToken);

		var planning = await PlanAsync(target, cancellationToken);
		return planning switch
		{
			Error error => OnPlanError(error, target.Provider),
			Success<WatchlistSyncPlan>(var plan) => BuildChanges(plan),
		};
	}

	private async Task<WatchlistSyncResult> ExecuteAsync(WatchlistSyncPlan plan, SyncTarget target, CancellationToken cancellationToken)
	{
		var pulledAdds = await repo.AddAsync(plan.Pull.Adds, new RemoteOrigin(target.Provider), cancellationToken);
		var pulledRemoves = await repo.RemoveAsync(plan.Pull.Removes, cancellationToken);

		await QueuePushesAsync(plan, cancellationToken);
		await PublishNotificationsAsync(pulledAdds, pulledRemoves, plan.Push, cancellationToken);

		logger.LogInformation(
			"Watchlist sync {Provider} complete: pulled {Pulled}, removed {Removed}, queued {Queued}",
			target.Provider,
			plan.Pull.Adds.Count,
			plan.Pull.Removes.Count,
			plan.Push.Count
		);
		return BuildChanges(plan);
	}

	private async Task PublishNotificationsAsync(
		IReadOnlyList<MediaId> pulledAdds,
		IReadOnlyList<MediaId> pulledRemoves,
		WatchlistDelta push,
		CancellationToken cancellationToken
	)
	{
		var now = clock.Now;
		if (pulledAdds.Count + pulledRemoves.Count + push.Adds.Count + push.Removes.Count > 0)
		{
			await publisher.PublishAsync(new WatchlistSyncedEvent(currentUser.Id, now, pulledAdds, pulledRemoves, push.Adds, push.Removes), cancellationToken);
		}
	}

	private async Task QueuePushesAsync(WatchlistSyncPlan plan, CancellationToken cancellationToken)
	{
		await outbox.EnqueueAsync(WatchlistOperationAction.Add, plan.Push.Adds, cancellationToken);
		await outbox.EnqueueAsync(WatchlistOperationAction.Remove, plan.Push.Removes, cancellationToken);

		foreach (var mediaId in plan.Push.All)
		{
			await publisher.PublishAsync(new WatchlistPushRequest(currentUser.Id, mediaId), cancellationToken);
		}
	}

	private WatchlistSyncRemoteFailed OnPlanError(Error error, WatchlistProvider provider)
	{
		logger.LogWarning("Remote fetch failed for {Provider}: {Reason}", provider, error.Reason);
		return new WatchlistSyncRemoteFailed(error.Reason);
	}

	private static WatchlistSyncSummary BuildChanges(WatchlistSyncPlan plan) =>
		new WatchlistSyncSummary(plan.Pull, plan.Push, [.. plan.NoOps, .. plan.Redundant]) { Unmatched = plan.Unmatched };

	private async Task<SyncTarget> TargetAsync(CancellationToken cancellationToken)
	{
		var provider = (await config.GetAsync(cancellationToken)).WatchlistProvider;
		return new SyncTarget(provider, provider.AsMediaProvider(), remotes[provider]);
	}

	private readonly record struct RemoteWatchlist(IReadOnlyList<MediaId> Resolved, IReadOnlyList<UnmatchedMedia> Unmatched);

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

	private async Task DropRedundantAsync(SyncTarget target, WatchlistSyncPlan plan, CancellationToken cancellationToken)
	{
		if (plan.Redundant.Count == 0)
		{
			return;
		}

		await outbox.DropAsync(plan.Redundant, cancellationToken);
		logger.LogInformation("Dropped {Count} watchlist outbox entries {Provider} already agrees with", plan.Redundant.Count, target.Provider);
	}

	private readonly record struct SyncTarget(WatchlistProvider Provider, MediaProvider MediaProvider, IWatchlistProvider Remote);
}
