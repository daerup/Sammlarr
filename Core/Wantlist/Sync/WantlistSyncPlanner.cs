using Contracts.Wantlist;
using Contracts.Wantlist.Outbox;
using Contracts.Wantlist.Sync;
using Domain.Providers;
using Domain.Wantlist;
using Domain.Wantlist.Outbox;
using Domain.Wantlist.Sync;
using Microsoft.Extensions.Logging;

namespace Core.Wantlist.Sync;

internal sealed class WantlistSyncPlanner(ActiveSink activeSink, IWantlistRepository repo, IWantlistOutbox outbox, ILogger<WantlistSyncPlanner> logger)
	: IWantlistSyncPlanner
{
	public async Task<WantlistPlanResult> PlanAsync(CancellationToken cancellationToken = default)
	{
		var sink = await activeSink.ResolveAsync(cancellationToken);
		return await PlanAsync(sink, cancellationToken)
			.ApplyError(error => logger.LogWarning("Reading {Sink} failed: {Reason}", sink.Kind, error.Reason))
			.Match(plan => (WantlistPlanResult)Project(plan), error => new WantlistSyncSinkUnreachable(error.Reason));
	}

	private async Task<Result<WantlistSyncPlan>> PlanAsync(IMediaSink sink, CancellationToken cancellationToken) =>
		await TryGetAsync(sink, cancellationToken)
			.Map(async snapshot =>
			{
				var wanted = new WantlistSnapshot(await repo.GetAllAsync(cancellationToken));
				var queued = new OutboxSnapshot(await outbox.GetAllAsync(cancellationToken));
				var plan = WantlistSyncPlan.Compute(snapshot, wanted, queued);
				await DropRedundantAsync(sink.Kind, plan, cancellationToken);
				return Success.Of(plan);
			});

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

	private static WantlistSyncSummary Project(WantlistSyncPlan plan) =>
		new WantlistSyncSummary(plan.Pull, plan.Push, [.. plan.NoOps, .. plan.Redundant]) { Unmatched = plan.Unmatched };
}
