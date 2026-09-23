using System.Diagnostics;
using Contracts;
using Contracts.Identity;
using Contracts.Messaging;
using Contracts.Settings;
using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Contracts.Watchlist.Sync;
using Domain.Media;
using Domain.Notifications.Events;
using Domain.Providers;
using Domain.Watchlist;
using Domain.Watchlist.Outbox;
using Domain.Watchlist.Sync;
using Microsoft.Extensions.Logging;
using Observability.Tracing;

namespace Core.Watchlist.Sync;

internal sealed class WatchlistSyncEngine(
	WatchlistSyncGate gate,
	IWatchlistSyncPlanner planner,
	IProviderConfigRepository config,
	IWatchlistRepository repo,
	IWatchlistOutbox outbox,
	IMessagePublisher publisher,
	IDateTimeProvider clock,
	ICurrentUser currentUser,
	ILogger<WatchlistSyncEngine> logger
) : IWatchlistSyncEngine
{
	public async Task<WatchlistSyncResult> SyncAsync(WatchlistSyncTrigger trigger, CancellationToken cancellationToken = default)
	{
		if (await gate.EnterAsync(currentUser.Id, cancellationToken) is not Some<IDisposable>(var lease))
		{
			return new WatchlistSyncAlreadyRunning();
		}

		using var _ = lease;
		using var activity = Instrumentation.Source.StartActivity(nameof(WatchlistSyncEngine));
		activity?.SetTag("sammlarr.sync.trigger", trigger.ToString());

		var provider = (await config.GetAsync(cancellationToken)).WatchlistProvider;
		activity?.SetTag("sammlarr.sync.provider", provider.ToString());

		var planning = await planner.PlanAsync(cancellationToken);
		WatchlistSyncResult syncResult = planning switch
		{
			WatchlistSyncRemoteFailed failed => failed,
			WatchlistSyncSummary plan => await ExecuteAsync(plan, provider, cancellationToken),
		};
		if (syncResult is WatchlistSyncRemoteFailed remoteFailed)
		{
			activity?.SetStatus(ActivityStatusCode.Error, remoteFailed.Reason);
			await publisher.PublishAsync(new WatchlistSyncFailedEvent(currentUser.Id, clock.Now, remoteFailed.Reason), cancellationToken);
		}
		return syncResult;
	}

	private async Task<WatchlistSyncResult> ExecuteAsync(WatchlistSyncSummary plan, WatchlistProvider provider, CancellationToken cancellationToken)
	{
		var pulledAdds = await repo.AddAsync(plan.Pull.Adds, new RemoteOrigin(provider), cancellationToken);
		var pulledRemoves = await repo.RemoveAsync(plan.Pull.Removes, cancellationToken);

		await QueuePushesAsync(plan.Push, cancellationToken);
		await PublishNotificationsAsync(pulledAdds, pulledRemoves, plan.Push, cancellationToken);

		logger.LogInformation(
			"Watchlist sync {Provider} complete: pulled {Pulled}, removed {Removed}, queued {Queued}",
			provider,
			plan.Pull.Adds.Count,
			plan.Pull.Removes.Count,
			plan.Push.Count
		);
		return plan;
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

	private async Task QueuePushesAsync(WatchlistDelta push, CancellationToken cancellationToken)
	{
		await outbox.EnqueueAsync(WatchlistOperationAction.Add, push.Adds, cancellationToken);
		await outbox.EnqueueAsync(WatchlistOperationAction.Remove, push.Removes, cancellationToken);

		foreach (var mediaId in push.All)
		{
			await publisher.PublishAsync(new WatchlistPushRequest(currentUser.Id, mediaId), cancellationToken);
		}
	}
}
