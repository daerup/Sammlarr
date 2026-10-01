using Contracts.Wantlist;
using Contracts.Wantlist.Outbox;
using Contracts.Wantlist.Sync;
using Domain.Providers;
using Domain.Wantlist;
using Domain.Wantlist.Sync;
using Microsoft.Extensions.Logging;

namespace Core.Wantlist.Sync;

internal sealed class WantlistSyncPlanner(ActiveSink activeSink, IWantlistRepository repo, IWantlistOutbox outbox, ILogger<WantlistSyncPlanner> logger)
	: IWantlistSyncPlanner
{
	public async Task<WantlistPlanResult> PlanAsync(CancellationToken cancellationToken = default)
	{
		var sink = await activeSink.ResolveAsync(cancellationToken);
		return await PlanAsync(sink, cancellationToken) switch
		{
			Error error => OnPlanError(error, sink.Kind),
			Success<WantlistSyncPlan>(var plan) => Project(plan),
		};
	}

	private async Task<Result<WantlistSyncPlan>> PlanAsync(IMediaSink sink, CancellationToken cancellationToken) =>
		await ReadAsync(sink, cancellationToken)
			.Map(async contents =>
			{
				var wanted = new WantlistSnapshot(await repo.GetAllAsync(cancellationToken));
				var outboxOps = await outbox.GetAsync(cancellationToken);
				var plan = WantlistSyncPlan.Compute(contents.Matched, [.. wanted.Ids], outboxOps) with { Unmatched = contents.Unmatched };
				await DropRedundantAsync(sink.Kind, plan, cancellationToken);
				return Success.Of(plan);
			});

	private static async Task<Result<SinkContents>> ReadAsync(IMediaSink sink, CancellationToken cancellationToken)
	{
		try
		{
			return Success.Of(await sink.ReadAsync(cancellationToken));
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

	private WantlistSyncSinkUnreachable OnPlanError(Error error, SinkKind sink)
	{
		logger.LogWarning("Reading {Sink} failed: {Reason}", sink, error.Reason);
		return new WantlistSyncSinkUnreachable(error.Reason);
	}

	private static WantlistSyncSummary Project(WantlistSyncPlan plan) =>
		new WantlistSyncSummary(plan.Pull, plan.Push, [.. plan.NoOps, .. plan.Redundant]) { Unmatched = plan.Unmatched };
}
