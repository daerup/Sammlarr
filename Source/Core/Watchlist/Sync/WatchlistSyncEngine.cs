using System.Diagnostics;
using Contracts;
using Contracts.Auth;
using Contracts.DI;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Contracts.Watchlist.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

			var fetch = await FetchRemoteAsync(deps, cancellationToken);
			if (fetch.FailureReason is { } reason)
			{
				logger.LogWarning("Remote fetch failed for {Provider}: {Reason}", deps.Provider, reason);
				var result = new WatchlistSyncRemoteFailed(reason);
				SammlarrTelemetry.TagWatchlistSyncOutcome(activity, result, stopwatch.ElapsedMilliseconds);
				return result;
			}

			var plan = await ComputePlanAsync(deps, fetch.RemoteIds!, cancellationToken);

			await deps.Repo.AddAsync(plan.InsertEntries, new WatchlistItemProvenance(new RemoteOrigin()), cancellationToken);
			await deps.Repo.RemoveAsync(plan.DeleteEntries, cancellationToken);
			await deps.Outbox.DropAsync(WatchlistOperationKind.Add, plan.DropOutboxAdds, cancellationToken);
			await deps.Outbox.DropAsync(WatchlistOperationKind.Remove, plan.DropOutboxRemoves, cancellationToken);

			using var drainCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			drainCts.CancelAfter(options.DrainBudget);

			var adds = await PushAsync(
				deps.Provider,
				plan.PushAdds,
				op: "add",
				apply: deps.RemoteWatchlistProvider.AddToWatchlistAsync,
				onSuccess: (id, ct) => deps.Outbox.DropAsync(WatchlistOperationKind.Add, [id], ct),
				drainCts.Token
			);

			var removes = await PushAsync(
				deps.Provider,
				plan.PushRemoves,
				op: "remove",
				apply: deps.RemoteWatchlistProvider.RemoveFromWatchlistAsync,
				onSuccess: (id, ct) => deps.Outbox.DropAsync(WatchlistOperationKind.Remove, [id], ct),
				drainCts.Token
			);

			var syncResult = BuildResult(plan, adds, removes);
			SammlarrTelemetry.TagWatchlistSyncOutcome(activity, syncResult, stopwatch.ElapsedMilliseconds);
			return syncResult;
		}
		finally
		{
			_gate.Release();
		}
	}

	public async Task<WatchlistSyncDryRun> DryRunAsync(CancellationToken cancellationToken = default)
	{
		await using var scope = scopeFactory.CreateAsyncScope();
		var deps = await ResolveAsync(scope, cancellationToken);

		var fetch = await FetchRemoteAsync(deps, cancellationToken);
		if (fetch.FailureReason is { } reason)
		{
			logger.LogWarning("Remote fetch failed during dry run for {Provider}: {Reason}", deps.Provider, reason);
			return new WatchlistSyncDryRunRemoteFailed(reason);
		}

		var plan = await ComputePlanAsync(deps, fetch.RemoteIds!, cancellationToken);

		var changes = plan
			.InsertEntries.Select(id => new WatchlistSyncChange(id, WatchlistSyncDirection.Pull, WatchlistOperationKind.Add))
			.Concat(plan.DeleteEntries.Select(id => new WatchlistSyncChange(id, WatchlistSyncDirection.Pull, WatchlistOperationKind.Remove)))
			.Concat(plan.PushAdds.Select(id => new WatchlistSyncChange(id, WatchlistSyncDirection.Push, WatchlistOperationKind.Add)))
			.Concat(plan.PushRemoves.Select(id => new WatchlistSyncChange(id, WatchlistSyncDirection.Push, WatchlistOperationKind.Remove)))
			.ToList();

		return new WatchlistSyncDryRunReport(changes, plan.InSync);
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
			RemoteWatchlistProvider: scope.ServiceProvider.GetRequiredService<IKeyedServiceIndex<WatchlistProvider, IWatchlistProvider>>()[provider]
		);
	}

	private static async Task<RemoteFetch> FetchRemoteAsync(EngineDeps deps, CancellationToken cancellationToken)
	{
		try
		{
			using var fetch = SammlarrTelemetry.StartWatchlistRemoteFetch(deps.Provider);
			var ids = await deps.RemoteWatchlistProvider.GetWatchlistAsync(cancellationToken).ToListAsync(cancellationToken);
			return RemoteFetch.Success(ids);
		}
		catch (Exception ex)
		{
			return RemoteFetch.Failure(ex.Message);
		}
	}

	private static async Task<WatchlistSyncPlan> ComputePlanAsync(EngineDeps deps, IReadOnlyList<IMediaId> remoteIds, CancellationToken cancellationToken)
	{
		var localIds = await deps.Repo.GetIdsAsync(deps.MediaProvider, cancellationToken);
		var outboxOps = await deps.Outbox.GetAsync(deps.MediaProvider, cancellationToken);
		return WatchlistSyncPlan.Compute(remoteIds, localIds, outboxOps);
	}

	private async Task<PushOutcome> PushAsync(
		WatchlistProvider provider,
		IEnumerable<IMediaId> ids,
		string op,
		Func<IMediaId, CancellationToken, Task<bool>> apply,
		Func<IMediaId, CancellationToken, Task> onSuccess,
		CancellationToken cancellationToken
	)
	{
		var pushed = 0;
		var failed = 0;
		foreach (var id in ids)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}
			using var push = SammlarrTelemetry.StartWatchlistPush(provider, op);
			bool ok;
			try
			{
				ok = await apply(id, cancellationToken);
				await onSuccess(id, cancellationToken);
				pushed++;
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "Push {Op} failed for {MediaId}", op, id);
				ok = false;
				failed++;
			}
			SammlarrTelemetry.TagWatchlistPushOutcome(push, ok);
		}
		return new PushOutcome(pushed, failed);
	}

	private static WatchlistSyncResult BuildResult(WatchlistSyncPlan plan, PushOutcome adds, PushOutcome removes)
	{
		if (
			plan is { InsertCount: 0, ConfirmCount: 0, HardDeleteCount: 0 }
            && adds.Pushed == 0
            && removes.Pushed == 0
            && adds.Failed == 0
            && removes.Failed == 0
		)
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
		IWatchlistProvider RemoteWatchlistProvider
	);

	private readonly record struct RemoteFetch(IReadOnlyList<IMediaId>? RemoteIds, string? FailureReason)
	{
		public static RemoteFetch Failure(string reason) => new(null, reason);

		public static RemoteFetch Success(IReadOnlyList<IMediaId> ids) => new(ids, null);
	}
}
