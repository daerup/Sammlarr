using System.Diagnostics;
using Contracts;
using Contracts.Identity;
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
		var down = DownOf(planning);
		down.Apply(unreachable => activity?.SetStatus(ActivityStatusCode.Error, unreachable.Reason));
		await down.Apply(unreachable =>
			announcer.AnnounceAsync(new WantlistSyncFailedNotification(currentUser.Id, clock.Now, unreachable.Reason), cancellationToken)
		);
		return syncResult;
	}

	private static Optional<WantlistSyncSinkUnreachable> DownOf(WantlistPlanResult planning) =>
		planning switch
		{
			WantlistSyncSinkUnreachable failed => Some.Of(failed),
			WantlistSyncSummary plan => plan.Down,
		};

	private async Task<WantlistSyncResult> ExecuteAsync(WantlistSyncSummary plan, IMediaSink sink, CancellationToken cancellationToken)
	{
		var pulledAdds = await repo.AddAsync(plan.Pull.Adds, new SinkOrigin(sink.Kind), cancellationToken);
		var pulledRemoves = await repo.RemoveAsync(plan.Pull.Removes, cancellationToken);
		if (pulledAdds.Count > 0 || pulledRemoves.Count > 0)
		{
			announcer.Announce(new WantlistChangedEvent(currentUser.Id, pulledAdds, pulledRemoves));
		}

		var pushed = await DrainAsync(sink, plan.Waiting.ToHashSet(), cancellationToken);
		await SyncNotification
			.For(currentUser.Id, clock.Now, pulledAdds, pulledRemoves, pushed)
			.Apply(synced => announcer.AnnounceAsync(synced, cancellationToken));

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

	private async Task<IReadOnlyList<PushOutcome>> DrainAsync(IMediaSink sink, IReadOnlySet<MediaId> waiting, CancellationToken cancellationToken)
	{
		var pending = new OutboxSnapshot(await outbox.GetAllAsync(sink.Kind, cancellationToken)).Pending.Where(entry => !waiting.Contains(entry.MediaId)).ToList();
		var addedBy = pending.Any(entry => entry.Action is WantlistOperationAction.Add) ? await AddingFeedsAsync(cancellationToken) : [];
		List<PushOutcome> outcomes = [];
		foreach (var batch in pending.GroupBy(entry => entry.Action))
		{
			outcomes.AddRange(await ApplyBatchAsync(sink, batch.Key, [.. batch], addedBy, cancellationToken));
		}

		return outcomes;
	}

	private async Task<Dictionary<MediaId, Domain.Feed.Feed>> AddingFeedsAsync(CancellationToken cancellationToken)
	{
		return (await repo.GetAllAsync(cancellationToken))
			.Select(item => AddingFeed(item.Provenance).Map(feed => (item.MediaId, Feed: feed)))
			.WhereSome()
			.ToDictionary(entry => entry.MediaId, entry => entry.Feed);
	}

	private static Optional<Domain.Feed.Feed> AddingFeed(WantlistItemProvenance provenance)
	{
		return provenance switch
		{
			FeedOrigin origin => Some.Of(origin.Feed),
			SinkOrigin or ManualOrigin or OrphanedOrigin => new None(),
		};
	}

	private async Task<IReadOnlyList<PushOutcome>> ApplyBatchAsync(
		IMediaSink sink,
		WantlistOperationAction action,
		IReadOnlyList<WantlistOutboxEntry> entries,
		IReadOnlyDictionary<MediaId, Domain.Feed.Feed> addedBy,
		CancellationToken cancellationToken
	)
	{
		var awaiting = entries.ToDictionary(entry => entry.MediaId);
		List<PushOutcome> outcomes = [];
		var pushes = awaiting.Keys.Select(id => new SinkPush(id, addedBy.GetValueOrNone(id))).ToList();
		await foreach (var ack in sink.ApplyAsync(action, pushes, cancellationToken))
		{
			var acked = awaiting[ack.MediaId];
			awaiting.Remove(ack.MediaId);
			outcomes.Add(await SettleAsync(sink.Kind, acked, KindFor(acked, ack.Outcome), ack.Reason, cancellationToken));
		}

		foreach (var unacked in awaiting.Values)
		{
			outcomes.Add(await SettleAsync(sink.Kind, unacked, KindFor(unacked, SinkSyncOutcome.Failed), new None(), cancellationToken));
		}

		return outcomes;
	}

	private async Task<PushOutcome> SettleAsync(
		SinkKind sink,
		WantlistOutboxEntry entry,
		WantlistOutboxEventKind kind,
		Optional<string> reason,
		CancellationToken cancellationToken
	)
	{
		if (kind is WantlistOutboxEventKind.Pushed)
		{
			await outbox.DropAsync(sink, [entry.MediaId], cancellationToken);
		}
		else
		{
			await outbox.RecordFailedAttemptAsync(
				sink,
				[entry.MediaId],
				kind is WantlistOutboxEventKind.Abandoned ? WantlistOutboxState.Failed : WantlistOutboxState.Pending,
				reason,
				cancellationToken
			);
		}

		var outcome = new PushOutcome(entry.MediaId, entry.Action, kind);
		announcer.Announce(new WantlistOutboxEvent(currentUser.Id, outcome.MediaId, outcome.Kind) { Reason = reason });
		return outcome;
	}

	private static WantlistOutboxEventKind KindFor(WantlistOutboxEntry entry, SinkSyncOutcome outcome) =>
		outcome switch
		{
			SinkSyncOutcome.Applied => WantlistOutboxEventKind.Pushed,
			SinkSyncOutcome.Unaddressable => WantlistOutboxEventKind.Abandoned,
			SinkSyncOutcome.Failed => entry.Attempts + 1 >= MaxAttempts ? WantlistOutboxEventKind.Abandoned : WantlistOutboxEventKind.Retrying,
		};
}
