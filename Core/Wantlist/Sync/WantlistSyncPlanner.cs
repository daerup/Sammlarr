using Contracts.Media;
using Contracts.Wantlist;
using Contracts.Wantlist.Outbox;
using Contracts.Wantlist.Sync;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist;
using Domain.Wantlist.Arr;
using Domain.Wantlist.Outbox;
using Domain.Wantlist.Sync;
using Microsoft.Extensions.Logging;

namespace Core.Wantlist.Sync;

internal sealed class WantlistSyncPlanner(
	ActiveSink activeSink,
	IWantlistRepository repo,
	IWantlistOutbox outbox,
	IMediaRepository media,
	ILogger<WantlistSyncPlanner> logger
) : IWantlistSyncPlanner
{
	public async Task<WantlistPlanResult> PlanAsync(CancellationToken cancellationToken = default)
	{
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
				var items = await repo.GetAllAsync(cancellationToken);
				var entries = await outbox.GetAllAsync(cancellationToken);
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
				var stored = await media.GetAsync([.. ids.Distinct()], cancellationToken);
				var planned = stored.Where(found => answered.Contains(found.Identity.Type)).Select(found => found.Identity.Id).ToHashSet();
				return (IReadOnlySet<MediaId>)ids.Where(id => !planned.Contains(id)).ToHashSet();
			},
			() => Task.FromResult<IReadOnlySet<MediaId>>(new HashSet<MediaId>())
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

		await outbox.DropAsync(plan.Redundant, cancellationToken);
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
