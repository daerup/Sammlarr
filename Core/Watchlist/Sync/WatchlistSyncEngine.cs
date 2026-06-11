using System.Diagnostics;
using Contracts;
using Contracts.Auth;
using Contracts.DI;
using Contracts.Media;
using Contracts.Media.Convertions;
using Contracts.Settings;
using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Contracts.Watchlist.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Options;
using Shared.Telemetry;

namespace Core.Watchlist.Sync;

internal sealed class WatchlistSyncEngine(IServiceScopeFactory scopeFactory, WatchlistSyncOptions options, ILogger<WatchlistSyncEngine> logger)
	: IWatchlistSyncEngine
{
	private readonly SemaphoreSlim _gate = new(1, 1);

	public async Task<WatchlistSyncResult> SyncAsync(WatchlistSyncTrigger trigger, CancellationToken cancellationToken = default)
	{
		if (!await _gate.WaitAsync(0, cancellationToken))
		{
			return new WatchlistSyncAlreadyRunning();
		}

		try
		{
			await using var scope = scopeFactory.CreateAsyncScope();
			var deps = await ResolveAsync(scope, cancellationToken);

			var stopwatch = Stopwatch.StartNew();
			using var activity = SammlarrTelemetry.StartWatchlistSync(deps.Provider, trigger);

			var planning = await PlanAsync(deps, cancellationToken);
			var syncResult = planning switch
			{
				Error error => OnPlanError(error, deps.Provider),
				WatchlistSyncPlan plan => await ExecutePlanAsync(plan, deps, cancellationToken),
			};
			SammlarrTelemetry.TagWatchlistSyncOutcome(activity, syncResult, stopwatch.ElapsedMilliseconds);
			return syncResult;
		}
		finally
		{
			_gate.Release();
		}
	}

	private WatchlistSyncResult OnPlanError(Error error, WatchlistProvider provider)
	{
		logger.LogWarning("Remote fetch failed for {Provider}: {Reason}", provider, error.Reason);
		return new WatchlistSyncRemoteFailed(error.Reason);
	}

	private async Task<WatchlistSyncResult> ExecutePlanAsync(WatchlistSyncPlan plan, EngineDeps deps, CancellationToken cancellationToken)
	{
		await deps.Repo.AddAsync(plan.InsertEntries, new WatchlistItemProvenance(new RemoteOrigin()), cancellationToken);
		await deps.Repo.RemoveAsync(plan.DeleteEntries, cancellationToken);
		await deps.Outbox.DropAsync(WatchlistOperationKind.Add, plan.DropOutboxAdds, cancellationToken);
		await deps.Outbox.DropAsync(WatchlistOperationKind.Remove, plan.DropOutboxRemoves, cancellationToken);

		var pushExternals = await deps.MediaRepo.GetExternalsAsync(plan.PushAdds.Concat(plan.PushRemoves), deps.MediaProvider, cancellationToken);

		using var drainCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		drainCts.CancelAfter(options.DrainBudget);

		var adds = await PushAsync(deps, plan.PushAdds, pushExternals, WatchlistOperationKind.Add, drainCts.Token);
		var removes = await PushAsync(deps, plan.PushRemoves, pushExternals, WatchlistOperationKind.Remove, drainCts.Token);

		return BuildResult(plan, adds, removes);
	}

	public async Task<Result<WatchlistSyncDryRun>> DryRunAsync(CancellationToken cancellationToken = default)
	{
		await using var scope = scopeFactory.CreateAsyncScope();
		var deps = await ResolveAsync(scope, cancellationToken);

		var planning = await PlanAsync(deps, cancellationToken);
		return planning switch
		{
			Error error => OnDryRunError(error, deps.Provider),
			WatchlistSyncPlan plan => BuildDryRun(plan),
		};
	}

	private Error OnDryRunError(Error error, WatchlistProvider provider)
	{
		logger.LogWarning("Remote fetch failed during dry run for {Provider}: {Reason}", provider, error.Reason);
		return error;
	}

	private static WatchlistSyncDryRun BuildDryRun(WatchlistSyncPlan plan)
	{
		var changes = plan
			.InsertEntries.Select(id => new WatchlistSyncChange(id, WatchlistSyncDirection.Pull, WatchlistOperationKind.Add))
			.Concat(plan.DeleteEntries.Select(id => new WatchlistSyncChange(id, WatchlistSyncDirection.Pull, WatchlistOperationKind.Remove)))
			.Concat(plan.PushAdds.Select(id => new WatchlistSyncChange(id, WatchlistSyncDirection.Push, WatchlistOperationKind.Add)))
			.Concat(plan.PushRemoves.Select(id => new WatchlistSyncChange(id, WatchlistSyncDirection.Push, WatchlistOperationKind.Remove)))
			.ToList();

		return new WatchlistSyncDryRun(changes, plan.InSync);
	}

	private static async Task<EngineDeps> ResolveAsync(AsyncServiceScope scope, CancellationToken cancellationToken)
	{
		var config = await scope.ServiceProvider.GetRequiredService<IProviderConfigRepository>().GetAsync(cancellationToken);
		var provider = config.WatchlistProvider;
		return new EngineDeps(
			Provider: provider,
			MediaProvider: provider.AsMediaProvider(),
			Repo: scope.ServiceProvider.GetRequiredService<IWatchlistRepository>(),
			Outbox: scope.ServiceProvider.GetRequiredService<IWatchlistOutbox>(),
			MediaRepo: scope.ServiceProvider.GetRequiredService<IMediaRepository>(),
			Media: scope.ServiceProvider.GetRequiredService<IMediaService>(),
			RemoteWatchlistProvider: scope.ServiceProvider.GetRequiredService<IKeyedServiceIndex<WatchlistProvider, IWatchlistProvider>>()[provider]
		);
	}

	private async Task<Result<IReadOnlyList<MediaId>>> FetchRemoteAsync(EngineDeps deps, CancellationToken cancellationToken)
	{
		try
		{
			using var fetch = SammlarrTelemetry.StartWatchlistRemoteFetch(deps.Provider);
			var externals = await deps.RemoteWatchlistProvider.GetWatchlistAsync(cancellationToken).ToListAsync(cancellationToken);
			var requests = externals.Select(e => new ResolveRequest([e], deps.MediaProvider, new None()));
			var remoteMediaIds = new List<MediaId>(externals.Count);
			var pipeline = deps.Media.IdentifyAsync(requests, cancellationToken).Zip(externals, cancellationToken);
			await foreach (var (result, external) in pipeline)
			{
				if (result is Identification identification)
				{
					remoteMediaIds.Add(identification.Id);
				}
				else
				{
					logger.LogWarning("Remote watchlist item {External} could not be resolved during sync", external);
				}
			}
			return remoteMediaIds;
		}
		catch (Exception ex)
		{
			return Error.From(ex);
		}
	}

	private async Task<Result<WatchlistSyncPlan>> PlanAsync(EngineDeps deps, CancellationToken cancellationToken)
	{
		var fetch = await FetchRemoteAsync(deps, cancellationToken);
		if (fetch is Error error)
		{
			return error;
		}
		var remoteIds = (IReadOnlyList<MediaId>)fetch.Value;
		var localIds = await deps.Repo.GetIdsAsync(cancellationToken);
		var outboxOps = await deps.Outbox.GetAsync(cancellationToken);
		return WatchlistSyncPlan.Compute(remoteIds, localIds, outboxOps);
	}

	private async Task<PushOutcome> PushAsync(
		EngineDeps deps,
		IEnumerable<MediaId> ids,
		IReadOnlyDictionary<MediaId, IExternalMediaId> externalsByMediaId,
		WatchlistOperationKind kind,
		CancellationToken cancellationToken
	)
	{
		var op = kind.ToString().ToLowerInvariant();
		var (pending, unpushable) = await PartitionAsync(deps, kind, ids, externalsByMediaId, op, cancellationToken);
		if (pending.Count == 0)
		{
			return new PushOutcome(0, unpushable);
		}
		var processed = await ApplyAndProcessAsync(deps, kind, pending, op, cancellationToken);
		var orphans = await SettleOrphansAsync(deps, kind, pending, cancellationToken);
		return processed with { Failed = unpushable + processed.Failed + orphans };
	}

	private async Task<(Dictionary<IExternalMediaId, MediaId> Pending, int Unpushable)> PartitionAsync(
		EngineDeps deps,
		WatchlistOperationKind kind,
		IEnumerable<MediaId> ids,
		IReadOnlyDictionary<MediaId, IExternalMediaId> externalsByMediaId,
		string op,
		CancellationToken cancellationToken
	)
	{
		var pending = new Dictionary<IExternalMediaId, MediaId>();
		var unpushable = new List<MediaId>();
		foreach (var id in ids)
		{
			if (externalsByMediaId.TryGetValue(id, out var external))
			{
				pending[external] = id;
				continue;
			}
			logger.LogWarning("No external id for media {MediaId}; skipping {Op} push", id, op);
			unpushable.Add(id);
		}
		await deps.Outbox.RecordFailureAsync(kind, unpushable, cancellationToken);
		return (pending, unpushable.Count);
	}

	private async Task<PushOutcome> ApplyAndProcessAsync(
		EngineDeps deps,
		WatchlistOperationKind kind,
		Dictionary<IExternalMediaId, MediaId> pending,
		string op,
		CancellationToken cancellationToken
	)
	{
		var apply = ResolveApply(deps, kind);
		var pushed = 0;
		var failed = 0;
		try
		{
			await foreach (var result in apply([.. pending.Keys], cancellationToken).WithCancellation(cancellationToken))
			{
				if (!pending.Remove(result.ExternalId, out var mediaId))
				{
					continue;
				}
				if (result.Success)
				{
					await OnPushSuccess(deps, kind, mediaId, op, cancellationToken);
					pushed++;
				}
				else
				{
					await OnPushFailure(deps, kind, mediaId, op, cancellationToken);
					failed++;
				}
			}
		}
		catch (OperationCanceledException)
		{
			pending.Clear();
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Push {Op} threw", op);
		}
		return new PushOutcome(pushed, failed);
	}

	private static async Task<int> SettleOrphansAsync(
		EngineDeps deps,
		WatchlistOperationKind kind,
		Dictionary<IExternalMediaId, MediaId> pending,
		CancellationToken cancellationToken
	)
	{
		var orphans = pending.Values.ToList();
		await deps.Outbox.RecordFailureAsync(kind, orphans, cancellationToken);
		return orphans.Count;
	}

	private static Func<IEnumerable<IExternalMediaId>, CancellationToken, IAsyncEnumerable<WatchlistPushResult>> ResolveApply(
		EngineDeps deps,
		WatchlistOperationKind kind
	) => kind == WatchlistOperationKind.Add ? deps.RemoteWatchlistProvider.AddToWatchlistAsync : deps.RemoteWatchlistProvider.RemoveFromWatchlistAsync;

	private async Task OnPushSuccess(EngineDeps deps, WatchlistOperationKind kind, MediaId mediaId, string op, CancellationToken cancellationToken)
	{
		using var push = SammlarrTelemetry.StartWatchlistPush(deps.Provider, op);
		await deps.Outbox.DropAsync(kind, [mediaId], cancellationToken);
		SammlarrTelemetry.TagWatchlistPushOutcome(push, true);
	}

	private async Task OnPushFailure(EngineDeps deps, WatchlistOperationKind kind, MediaId mediaId, string op, CancellationToken cancellationToken)
	{
		using var push = SammlarrTelemetry.StartWatchlistPush(deps.Provider, op);
		await deps.Outbox.RecordFailureAsync(kind, [mediaId], cancellationToken);
		SammlarrTelemetry.TagWatchlistPushOutcome(push, false);
	}

	private static WatchlistSyncResult BuildResult(WatchlistSyncPlan plan, PushOutcome adds, PushOutcome removes)
	{
		if (plan is { InsertCount: 0, ConfirmCount: 0, HardDeleteCount: 0 } && adds.Pushed == 0 && removes.Pushed == 0 && adds.Failed == 0 && removes.Failed == 0)
		{
			return new WatchlistSyncUnchanged();
		}
		return new WatchlistSyncSuccess(plan.InsertCount, plan.ConfirmCount, plan.HardDeleteCount, adds.Pushed, removes.Pushed, adds.Failed + removes.Failed);
	}

	private readonly record struct PushOutcome(int Pushed, int Failed);

	private readonly record struct EngineDeps(
		WatchlistProvider Provider,
		MediaProvider MediaProvider,
		IWatchlistRepository Repo,
		IWatchlistOutbox Outbox,
		IMediaRepository MediaRepo,
		IMediaService Media,
		IWatchlistProvider RemoteWatchlistProvider
	);
}
