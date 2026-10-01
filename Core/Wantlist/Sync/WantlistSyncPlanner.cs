using Contracts.Media;
using Contracts.Wantlist;
using Contracts.Wantlist.Outbox;
using Contracts.Wantlist.Sync;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist.Sync;
using Microsoft.Extensions.Logging;

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
		var sink = await activeSink.ResolveAsync(cancellationToken);
		return await PlanAsync(sink, cancellationToken) switch
		{
			Error error => OnPlanError(error, sink.Kind),
			Success<WantlistSyncPlan>(var plan) => Project(plan),
		};
	}

	private async Task<Result<WantlistSyncPlan>> PlanAsync(IMediaSink sink, CancellationToken cancellationToken) =>
		await FetchRemoteAsync(sink, cancellationToken)
			.Map(async remote =>
			{
				var localIds = await repo.GetIdsAsync(cancellationToken);
				var outboxOps = await outbox.GetAsync(cancellationToken);
				var plan = WantlistSyncPlan.Compute(remote.Resolved, localIds, outboxOps) with { Unmatched = remote.Unmatched };
				await DropRedundantAsync(sink.Kind, plan, cancellationToken);
				return Success.Of(plan);
			});

	private async Task<Result<RemoteWantlist>> FetchRemoteAsync(IMediaSink sink, CancellationToken cancellationToken)
	{
		try
		{
			var entries = await sink.GetWantlistAsync(cancellationToken).ToListAsync(cancellationToken);
			var requests = entries.Select(entry => new ResolveRequest([.. entry.Externals], sink.Kind.AsMediaProvider(), new None()));
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
					logger.LogWarning("Remote wantlist item {Title} could not be resolved during sync", entry.Title ?? entry.Externals.FirstOrDefault()?.Value);
				}
			}
			return Success.Of(new RemoteWantlist(remoteMediaIds, unmatched));
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

	private readonly record struct RemoteWantlist(IReadOnlyList<MediaId> Resolved, IReadOnlyList<UnmatchedMedia> Unmatched);
}
