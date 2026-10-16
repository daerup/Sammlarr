using Contracts.Media;
using Contracts.Wantlist;
using Contracts.Wantlist.Outbox;
using Contracts.Wantlist.Sync;
using Domain.Media;
using Domain.Providers;
using Domain.Usage;
using Domain.Wantlist;
using Domain.Wantlist.Arr;
using Domain.Wantlist.Outbox;
using Domain.Wantlist.Sync;
using Microsoft.Extensions.Logging;
using Observability.Usage;

namespace Core.Wantlist.Sync;

internal sealed class WantlistSyncPlanner(
	ActiveSink activeSink,
	IWantlistRepository repo,
	IWantlistOutbox outbox,
	IMediaService media,
	ILogger<WantlistSyncPlanner> logger
) : IWantlistSyncPlanner
{
	public async Task<WantlistPlanResult> PlanAsync(CancellationToken cancellationToken = default)
	{
		using var marked = UsageCauses.Mark(UsageCause.WantlistSync);
		var sink = await activeSink.ResolveAsync(cancellationToken);
		return await PlanAsync(sink, cancellationToken)
			.ApplyError(error => logger.LogWarning("Reading {Sink} failed: {Reason}", sink.Kind, error.Reason))
			.Match(
				summary => (WantlistPlanResult)summary,
				error => new WantlistSyncSinkUnreachable(error.Reason, error.CausedBy<ArrAppsUnreachableException>().Map(failed => failed.Apps).GetValueOr([]))
			);
	}

	private async Task<Result<WantlistSyncSummary>> PlanAsync(IMediaSink sink, CancellationToken cancellationToken)
	{
		return await TryGetAsync(sink, cancellationToken)
			.Map(async snapshot =>
			{
				cancellationToken.ThrowIfCancellationRequested();
				var items = await repo.GetAllAsync(cancellationToken);
				var entries = await outbox.GetAllAsync(sink.Kind, cancellationToken);
				var waiting = await WaitingAsync(snapshot.Down, [.. items.Select(item => item.MediaId), .. entries.Select(entry => entry.MediaId)], cancellationToken);
				var wanted = new WantlistSnapshot([.. items.Where(item => !waiting.Contains(item.MediaId))]);
				var queued = new OutboxSnapshot([.. entries.Where(entry => !waiting.Contains(entry.MediaId))]);
				var plan = WantlistSyncPlan.Compute(snapshot, wanted, queued);
				await DropRedundantAsync(sink.Kind, plan, cancellationToken);
				var held = entries.Where(entry => entry.Status is WantlistOutboxState.Pending && waiting.Contains(entry.MediaId));
				return Success.Of(Project(plan, snapshot.Down, [.. held.Select(entry => entry.MediaId)]));
			});
	}

	private async Task<IReadOnlySet<MediaId>> WaitingAsync(
		Optional<WantlistSyncSinkUnreachable> down,
		IReadOnlyList<MediaId> ids,
		CancellationToken cancellationToken
	)
	{
		return await down.Match(
			async unreachable =>
			{
				var answered = Enum.GetValues<ArrApp>().Except(unreachable.Apps).Select(app => app.Type).ToHashSet();
				var types = await media.GetTypesAsync(ids, cancellationToken);
				var planned = types.Where(type => answered.Contains(type.Value)).Select(type => type.Key).ToHashSet();
				return ids.Where(id => !planned.Contains(id)).ToHashSet();
			},
			() => []
		);
	}

	private static async Task<Result<SinkSnapshot>> TryGetAsync(IMediaSink sink, CancellationToken cancellationToken)
	{
		try
		{
			return Success.Of(await sink.GetAsync(cancellationToken));
		}
		catch (Exception ex)
		{
			return Error.From(ex);
		}
	}

	private async Task DropRedundantAsync(SinkKind sink, WantlistSyncPlan plan, CancellationToken cancellationToken)
	{
		if (plan.Redundant.Count == 0)
		{
			return;
		}

		await outbox.DropAsync(sink, plan.Redundant, cancellationToken);
		logger.LogInformation("Dropped {Count} wantlist outbox entries {Sink} already agrees with", plan.Redundant.Count, sink);
	}

	private static WantlistSyncSummary Project(WantlistSyncPlan plan, Optional<WantlistSyncSinkUnreachable> down, IReadOnlyList<MediaId> waiting)
	{
		return new WantlistSyncSummary(plan.Pull, plan.Push, [.. plan.NoOps, .. plan.Redundant])
		{
			Unmatched = plan.Unmatched,
			Down = down,
			Waiting = waiting,
		};
	}
}
