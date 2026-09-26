using System.Diagnostics;
using Contracts.DI;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Contracts.Watchlist.Sync;
using Domain.Media;
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
	IMediaRepository media,
	IKeyedServiceIndex<WatchlistProvider, IWatchlistProvider> providers,
	WatchlistSyncAnnouncer announcer,
	ICurrentUser currentUser,
	ILogger<WatchlistSyncEngine> logger
) : IWatchlistSyncEngine
{
	private const int MaxAttempts = 5;

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
			await announcer.NotifySyncFailedAsync(remoteFailed.Reason, cancellationToken);
		}
		return syncResult;
	}

	private async Task<WatchlistSyncResult> ExecuteAsync(WatchlistSyncSummary plan, WatchlistProvider provider, CancellationToken cancellationToken)
	{
		var pulledAdds = await repo.AddAsync(plan.Pull.Adds, new RemoteOrigin(provider), cancellationToken);
		var pulledRemoves = await repo.RemoveAsync(plan.Pull.Removes, cancellationToken);

		var pushed = await DrainAsync(provider, cancellationToken);
		await announcer.NotifySyncedAsync(pulledAdds, pulledRemoves, pushed, cancellationToken);

		logger.LogInformation(
			"Watchlist sync {Provider} complete: pulled {Pulled}, removed {Removed}, pushed {Pushed}, failed {Failed}",
			provider,
			plan.Pull.Adds.Count,
			plan.Pull.Removes.Count,
			pushed.Count(outcome => outcome.Kind is WatchlistOutboxEventKind.Pushed),
			pushed.Count(outcome => outcome.Kind is not WatchlistOutboxEventKind.Pushed)
		);
		return plan;
	}

	private async Task<IReadOnlyList<PushOutcome>> DrainAsync(WatchlistProvider provider, CancellationToken cancellationToken)
	{
		var pending = (await outbox.GetAsync(cancellationToken)).Where(entry => entry.Status is WatchlistOutboxState.Pending).ToList();
		if (pending.Count == 0)
		{
			return [];
		}

		var externals = await media.GetExternalsAsync([.. pending.Select(entry => entry.MediaId)], provider.AsMediaProvider(), cancellationToken);
		List<PushOutcome> outcomes = [];
		foreach (var stranded in pending.Where(entry => !externals.ContainsKey(entry.MediaId)))
		{
			outcomes.Add(await SettleAsync(stranded, WatchlistOutboxEventKind.Abandoned, cancellationToken));
		}

		foreach (var batch in pending.Where(entry => externals.ContainsKey(entry.MediaId)).GroupBy(entry => entry.Action))
		{
			outcomes.AddRange(await PushBatchAsync(providers[provider], batch.Key, [.. batch], externals, cancellationToken));
		}

		return outcomes;
	}

	private async Task<IReadOnlyList<PushOutcome>> PushBatchAsync(
		IWatchlistProvider provider,
		WatchlistOperationAction action,
		IReadOnlyList<WatchlistOutboxEntry> entries,
		IReadOnlyDictionary<MediaId, IExternalMediaId> externals,
		CancellationToken cancellationToken
	)
	{
		var awaiting = entries.ToDictionary(entry => externals[entry.MediaId].Value);
		List<PushOutcome> outcomes = [];
		await foreach (var ack in Send(provider, action, [.. entries.Select(entry => externals[entry.MediaId])], cancellationToken))
		{
			var acked = awaiting[ack.ExternalId.Value];
			awaiting.Remove(ack.ExternalId.Value);
			outcomes.Add(await SettleAsync(acked, KindFor(acked, ack.Success), cancellationToken));
		}

		foreach (var unacked in awaiting.Values)
		{
			outcomes.Add(await SettleAsync(unacked, KindFor(unacked, delivered: false), cancellationToken));
		}

		return outcomes;
	}

	private async Task<PushOutcome> SettleAsync(WatchlistOutboxEntry entry, WatchlistOutboxEventKind kind, CancellationToken cancellationToken)
	{
		if (kind is WatchlistOutboxEventKind.Pushed)
		{
			await outbox.DropAsync([entry.MediaId], cancellationToken);
		}
		else
		{
			await outbox.RecordFailedAttemptAsync(
				[entry.MediaId],
				kind is WatchlistOutboxEventKind.Abandoned ? WatchlistOutboxState.Failed : WatchlistOutboxState.Pending,
				cancellationToken
			);
		}

		var outcome = new PushOutcome(entry.MediaId, entry.Action, kind);
		announcer.NotifyPushed(outcome);
		return outcome;
	}

	private static WatchlistOutboxEventKind KindFor(WatchlistOutboxEntry entry, bool delivered) =>
		delivered ? WatchlistOutboxEventKind.Pushed
		: entry.Attempts + 1 >= MaxAttempts ? WatchlistOutboxEventKind.Abandoned
		: WatchlistOutboxEventKind.Retrying;

	private static IAsyncEnumerable<WatchlistPushAck> Send(
		IWatchlistProvider provider,
		WatchlistOperationAction action,
		IReadOnlyList<IExternalMediaId> externals,
		CancellationToken cancellationToken
	) =>
		action switch
		{
			WatchlistOperationAction.Add => provider.AddToWatchlistAsync(externals, cancellationToken),
			WatchlistOperationAction.Remove => provider.RemoveFromWatchlistAsync(externals, cancellationToken),
		};
}
