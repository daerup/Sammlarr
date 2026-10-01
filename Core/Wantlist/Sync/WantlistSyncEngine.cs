using System.Diagnostics;
using Contracts;
using Contracts.Identity;
using Contracts.Media;
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
	ActiveSink activeSink,
	IWantlistRepository repo,
	IWantlistOutbox outbox,
	IMediaRepository media,
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

		var sink = await activeSink.ResolveAsync(cancellationToken);
		activity?.SetTag("sammlarr.sync.sink", sink.Kind.ToString());

		var planning = await planner.PlanAsync(cancellationToken);
		WantlistSyncResult syncResult = planning switch
		{
			WantlistSyncSinkUnreachable failed => failed,
			WantlistSyncSummary plan => await ExecuteAsync(plan, sink, cancellationToken),
		};
		if (syncResult is WantlistSyncSinkUnreachable unreachable)
		{
			activity?.SetStatus(ActivityStatusCode.Error, unreachable.Reason);
			await announcer.AnnounceAsync(new WantlistSyncFailedNotification(currentUser.Id, clock.Now, unreachable.Reason), cancellationToken);
		}
		return syncResult;
	}

	private async Task<WantlistSyncResult> ExecuteAsync(WantlistSyncSummary plan, IMediaSink sink, CancellationToken cancellationToken)
	{
		var pulledAdds = await repo.AddAsync(plan.Pull.Adds, new SinkOrigin(sink.Kind), cancellationToken);
		var pulledRemoves = await repo.RemoveAsync(plan.Pull.Removes, cancellationToken);

		var pushed = await DrainAsync(sink, cancellationToken);
		if (SyncNotification.For(currentUser.Id, clock.Now, pulledAdds, pulledRemoves, pushed) is { } synced)
		{
			await announcer.AnnounceAsync(synced, cancellationToken);
		}

		logger.LogInformation(
			"Wantlist sync {Sink} complete: pulled {Pulled}, removed {Removed}, pushed {Pushed}, failed {Failed}",
			sink.Kind,
			plan.Pull.Adds.Count,
			plan.Pull.Removes.Count,
			pushed.Count(outcome => outcome.Kind is WantlistOutboxEventKind.Pushed),
			pushed.Count(outcome => outcome.Kind is not WantlistOutboxEventKind.Pushed)
		);
		return plan;
	}

	private async Task<IReadOnlyList<PushOutcome>> DrainAsync(IMediaSink sink, CancellationToken cancellationToken)
	{
		var pending = (await outbox.GetAsync(cancellationToken)).Where(entry => entry.Status is WantlistOutboxState.Pending).ToList();
		if (pending.Count == 0)
		{
			return [];
		}

		var externals = await media.GetExternalsAsync([.. pending.Select(entry => entry.MediaId)], sink.Kind.AsMediaProvider(), cancellationToken);
		List<PushOutcome> outcomes = [];
		foreach (var stranded in pending.Where(entry => !externals.ContainsKey(entry.MediaId)))
		{
			outcomes.Add(await SettleAsync(stranded, WantlistOutboxEventKind.Abandoned, cancellationToken));
		}

		foreach (var batch in pending.Where(entry => externals.ContainsKey(entry.MediaId)).GroupBy(entry => entry.Action))
		{
			outcomes.AddRange(await PushBatchAsync(sink, batch.Key, [.. batch], externals, cancellationToken));
		}

		return outcomes;
	}

	private async Task<IReadOnlyList<PushOutcome>> PushBatchAsync(
		IMediaSink sink,
		WantlistOperationAction action,
		IReadOnlyList<WantlistOutboxEntry> entries,
		IReadOnlyDictionary<MediaId, IExternalMediaId> externals,
		CancellationToken cancellationToken
	)
	{
		var awaiting = entries.ToDictionary(entry => externals[entry.MediaId].Value);
		List<PushOutcome> outcomes = [];
		await foreach (var ack in Send(sink, action, [.. entries.Select(entry => externals[entry.MediaId])], cancellationToken))
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
		IMediaSink sink,
		WantlistOperationAction action,
		IReadOnlyList<IExternalMediaId> externals,
		CancellationToken cancellationToken
	) =>
		action switch
		{
			WantlistOperationAction.Add => sink.AddToWantlistAsync(externals, cancellationToken),
			WantlistOperationAction.Remove => sink.RemoveFromWantlistAsync(externals, cancellationToken),
		};
}
