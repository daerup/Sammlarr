using System.Diagnostics;
using Contracts;
using Contracts.DI;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Wantlist;
using Contracts.Wantlist.Outbox;
using Contracts.Wantlist.Sync;
using Core.Announcing;
using Domain.Media;
using Domain.Notifications;
using Domain.Providers;
using Domain.Wantlist;
using Domain.Wantlist.Outbox;
using Domain.Wantlist.Sync;
using Microsoft.Extensions.Logging;
using Observability.Tracing;

namespace Core.Wantlist.Sync;

internal sealed class WantlistSyncEngine(
	WantlistSyncGate gate,
	IWantlistSyncPlanner planner,
	IProviderConfigService config,
	IWantlistRepository repo,
	IWantlistOutbox outbox,
	IMediaRepository media,
	IKeyedServiceIndex<SinkKind, IMediaSink> providers,
	Announcer announcer,
	ICurrentUser currentUser,
	IDateTimeProvider clock,
	ILogger<WantlistSyncEngine> logger
) : IWantlistSyncEngine
{
	private const int MaxAttempts = 5;

	public async Task<WantlistSyncResult> SyncAsync(WantlistSyncTrigger trigger, CancellationToken cancellationToken = default)
	{
		if (await gate.EnterAsync(currentUser.Id, cancellationToken) is not Some<IDisposable>(var lease))
		{
			return new WantlistSyncAlreadyRunning();
		}

		using var _ = lease;
		using var activity = Instrumentation.Source.StartActivity(nameof(WantlistSyncEngine));
		activity?.SetTag("sammlarr.sync.trigger", trigger.ToString());

		var provider = (await config.GetAsync(cancellationToken)).Sink;
		activity?.SetTag("sammlarr.sync.provider", provider.ToString());

		var planning = await planner.PlanAsync(cancellationToken);
		WantlistSyncResult syncResult = planning switch
		{
			WantlistSyncSinkUnreachable failed => failed,
			WantlistSyncSummary plan => await ExecuteAsync(plan, provider, cancellationToken),
		};
		if (syncResult is WantlistSyncSinkUnreachable remoteFailed)
		{
			activity?.SetStatus(ActivityStatusCode.Error, remoteFailed.Reason);
			await announcer.AnnounceAsync(new WantlistSyncFailedNotification(currentUser.Id, clock.Now, remoteFailed.Reason), cancellationToken);
		}
		return syncResult;
	}

	private async Task<WantlistSyncResult> ExecuteAsync(WantlistSyncSummary plan, SinkKind provider, CancellationToken cancellationToken)
	{
		var pulledAdds = await repo.AddAsync(plan.Pull.Adds, new SinkOrigin(provider), cancellationToken);
		var pulledRemoves = await repo.RemoveAsync(plan.Pull.Removes, cancellationToken);

		var pushed = await DrainAsync(provider, cancellationToken);
		if (SyncNotification.For(currentUser.Id, clock.Now, pulledAdds, pulledRemoves, pushed) is { } synced)
		{
			await announcer.AnnounceAsync(synced, cancellationToken);
		}

		logger.LogInformation(
			"Wantlist sync {Provider} complete: pulled {Pulled}, removed {Removed}, pushed {Pushed}, failed {Failed}",
			provider,
			plan.Pull.Adds.Count,
			plan.Pull.Removes.Count,
			pushed.Count(outcome => outcome.Kind is WantlistOutboxEventKind.Pushed),
			pushed.Count(outcome => outcome.Kind is not WantlistOutboxEventKind.Pushed)
		);
		return plan;
	}

	private async Task<IReadOnlyList<PushOutcome>> DrainAsync(SinkKind provider, CancellationToken cancellationToken)
	{
		var pending = (await outbox.GetAsync(cancellationToken)).Where(entry => entry.Status is WantlistOutboxState.Pending).ToList();
		if (pending.Count == 0)
		{
			return [];
		}

		var externals = await media.GetExternalsAsync([.. pending.Select(entry => entry.MediaId)], provider.AsMediaProvider(), cancellationToken);
		List<PushOutcome> outcomes = [];
		foreach (var stranded in pending.Where(entry => !externals.ContainsKey(entry.MediaId)))
		{
			outcomes.Add(await SettleAsync(stranded, WantlistOutboxEventKind.Abandoned, cancellationToken));
		}

		foreach (var batch in pending.Where(entry => externals.ContainsKey(entry.MediaId)).GroupBy(entry => entry.Action))
		{
			outcomes.AddRange(await PushBatchAsync(providers[provider], batch.Key, [.. batch], externals, cancellationToken));
		}

		return outcomes;
	}

	private async Task<IReadOnlyList<PushOutcome>> PushBatchAsync(
		IMediaSink provider,
		WantlistOperationAction action,
		IReadOnlyList<WantlistOutboxEntry> entries,
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

	private async Task<PushOutcome> SettleAsync(WantlistOutboxEntry entry, WantlistOutboxEventKind kind, CancellationToken cancellationToken)
	{
		if (kind is WantlistOutboxEventKind.Pushed)
		{
			await outbox.DropAsync([entry.MediaId], cancellationToken);
		}
		else
		{
			await outbox.RecordFailedAttemptAsync(
				[entry.MediaId],
				kind is WantlistOutboxEventKind.Abandoned ? WantlistOutboxState.Failed : WantlistOutboxState.Pending,
				cancellationToken
			);
		}

		var outcome = new PushOutcome(entry.MediaId, entry.Action, kind);
		announcer.Announce(new WantlistOutboxEvent(currentUser.Id, outcome.MediaId, outcome.Kind));
		return outcome;
	}

	private static WantlistOutboxEventKind KindFor(WantlistOutboxEntry entry, bool delivered) =>
		delivered ? WantlistOutboxEventKind.Pushed
		: entry.Attempts + 1 >= MaxAttempts ? WantlistOutboxEventKind.Abandoned
		: WantlistOutboxEventKind.Retrying;

	private static IAsyncEnumerable<SinkSyncAck> Send(
		IMediaSink provider,
		WantlistOperationAction action,
		IReadOnlyList<IExternalMediaId> externals,
		CancellationToken cancellationToken
	) =>
		action switch
		{
			WantlistOperationAction.Add => provider.AddToWantlistAsync(externals, cancellationToken),
			WantlistOperationAction.Remove => provider.RemoveFromWantlistAsync(externals, cancellationToken),
		};
}
