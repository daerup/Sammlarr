using Contracts.DI;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Wantlist;
using Contracts.Wantlist.Outbox;
using Contracts.Wantlist.Sync;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist.Sync;
using Microsoft.Extensions.Logging;

namespace Core.Wantlist.Sync;

internal sealed class WantlistSyncPlanner(
	IProviderConfigService config,
	IWantlistRepository repo,
	IWantlistOutbox outbox,
	IMediaService media,
	IKeyedServiceIndex<SinkKind, IMediaSink> remotes,
	ILogger<WantlistSyncPlanner> logger
) : IWantlistSyncPlanner
{
	public async Task<WantlistPlanResult> PlanAsync(CancellationToken cancellationToken = default)
	{
		var target = await TargetAsync(cancellationToken);
		return await PlanAsync(target, cancellationToken) switch
		{
			Error error => OnPlanError(error, target.Provider),
			Success<WantlistSyncPlan>(var plan) => Project(plan),
		};
	}

	private async Task<SyncTarget> TargetAsync(CancellationToken cancellationToken)
	{
		var provider = (await config.GetAsync(cancellationToken)).Sink;
		return new SyncTarget(provider, provider.AsMediaProvider(), remotes[provider]);
	}

	private async Task<Result<WantlistSyncPlan>> PlanAsync(SyncTarget target, CancellationToken cancellationToken) =>
		await FetchRemoteAsync(target, cancellationToken)
			.Map(async remote =>
			{
				var localIds = await repo.GetIdsAsync(cancellationToken);
				var outboxOps = await outbox.GetAsync(cancellationToken);
				var plan = WantlistSyncPlan.Compute(remote.Resolved, localIds, outboxOps) with { Unmatched = remote.Unmatched };
				await DropRedundantAsync(target, plan, cancellationToken);
				return Success.Of(plan);
			});

	private async Task<Result<RemoteWantlist>> FetchRemoteAsync(SyncTarget target, CancellationToken cancellationToken)
	{
		try
		{
			var entries = await target.Remote.GetWantlistAsync(cancellationToken).ToListAsync(cancellationToken);
			var requests = entries.Select(entry => new ResolveRequest([.. entry.Externals], target.MediaProvider, new None()));
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

	private async Task DropRedundantAsync(SyncTarget target, WantlistSyncPlan plan, CancellationToken cancellationToken)
	{
		if (plan.Redundant.Count == 0)
		{
			return;
		}

		await outbox.DropAsync(plan.Redundant, cancellationToken);
		logger.LogInformation("Dropped {Count} wantlist outbox entries {Provider} already agrees with", plan.Redundant.Count, target.Provider);
	}

	private WantlistSyncSinkUnreachable OnPlanError(Error error, SinkKind provider)
	{
		logger.LogWarning("Remote fetch failed for {Provider}: {Reason}", provider, error.Reason);
		return new WantlistSyncSinkUnreachable(error.Reason);
	}

	private static WantlistSyncSummary Project(WantlistSyncPlan plan) =>
		new WantlistSyncSummary(plan.Pull, plan.Push, [.. plan.NoOps, .. plan.Redundant]) { Unmatched = plan.Unmatched };

	private readonly record struct RemoteWantlist(IReadOnlyList<MediaId> Resolved, IReadOnlyList<UnmatchedMedia> Unmatched);

	private readonly record struct SyncTarget(SinkKind Provider, MediaProvider MediaProvider, IMediaSink Remote);
}
