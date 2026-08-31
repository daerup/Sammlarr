using System.Diagnostics;
using Contracts;
using Contracts.DI;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Messaging;
using Contracts.Settings;
using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Contracts.Watchlist.Sync;
using Domain.Identity;
using Domain.Media;
using Domain.Notifications.Events;
using Domain.Providers;
using Domain.Watchlist;
using Domain.Watchlist.Outbox;
using Domain.Watchlist.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Observability.Tracing;
using Shared;

namespace Core.Watchlist.Sync;

internal sealed class WatchlistSyncEngine(IServiceScopeFactory scopeFactory, ILogger<WatchlistSyncEngine> logger) : IWatchlistSyncEngine
{
	private readonly SemaphoreSlim _gate = new(1, 1);

	public async Task<WatchlistSyncResult> SyncAsync(WatchlistSyncTrigger trigger, ICurrentUser user, CancellationToken cancellationToken = default)
	{
		if (!await _gate.WaitAsync(0, cancellationToken))
		{
			return new WatchlistSyncAlreadyRunning();
		}

		using var activity = Instrumentation.Source.StartActivity(nameof(WatchlistSyncEngine));
		activity?.SetTag("sammlarr.sync.trigger", trigger.ToString());
		try
		{
			await using var scope = scopeFactory.CreateAsyncScope();
			using var _ = scope.ServiceProvider.GetRequiredService<IUserScope>().As(user);
			var deps = await ResolveAsync(scope, cancellationToken);
			activity?.SetTag("sammlarr.sync.provider", deps.Provider.ToString());

			var planning = await PlanAsync(deps, cancellationToken);
			var syncResult = planning switch
			{
				Error error => OnPlanError(error, deps.Provider),
				Success<WatchlistSyncPlan>(var plan) => await ExecuteAsync(plan, deps, user.Id, cancellationToken),
			};
			if (syncResult is WatchlistSyncRemoteFailed remoteFailed)
			{
				activity?.SetStatus(ActivityStatusCode.Error, remoteFailed.Reason);
			}
			return syncResult;
		}
		finally
		{
			_gate.Release();
		}
	}

	public async Task<WatchlistSyncResult> DryRunAsync(ICurrentUser user, CancellationToken cancellationToken = default)
	{
		if (!await _gate.WaitAsync(0, cancellationToken))
		{
			return new WatchlistSyncAlreadyRunning();
		}

		try
		{
			await using var scope = scopeFactory.CreateAsyncScope();
			using var _ = scope.ServiceProvider.GetRequiredService<IUserScope>().As(user);
			var deps = await ResolveAsync(scope, cancellationToken);

			var planning = await PlanAsync(deps, cancellationToken);
			return planning switch
			{
				Error error => OnPlanError(error, deps.Provider),
				Success<WatchlistSyncPlan>(var plan) => BuildChanges(plan),
			};
		}
		finally
		{
			_gate.Release();
		}
	}

	private async Task<WatchlistSyncResult> ExecuteAsync(WatchlistSyncPlan plan, EngineDeps deps, UserId userId, CancellationToken cancellationToken)
	{
		var pulledAdds = await deps.Repo.AddAsync(plan.Pull.Adds, new RemoteOrigin(deps.Provider), cancellationToken);
		var pulledRemoves = await deps.Repo.RemoveAsync(plan.Pull.Removes, cancellationToken);
		await deps.Outbox.DropAsync(plan.Redundant, cancellationToken);

		await QueuePushesAsync(deps, plan, userId, cancellationToken);
		await PublishNotificationsAsync(deps, userId, pulledAdds, pulledRemoves, plan.Push, cancellationToken);

		logger.LogInformation(
			"Watchlist sync {Provider} complete: pulled {Pulled}, removed {Removed}, queued {Queued}",
			deps.Provider,
			plan.Pull.Adds.Count,
			plan.Pull.Removes.Count,
			plan.Push.Count
		);
		return BuildChanges(plan);
	}

	private static async Task PublishNotificationsAsync(
		EngineDeps deps,
		UserId userId,
		IReadOnlyList<MediaId> pulledAdds,
		IReadOnlyList<MediaId> pulledRemoves,
		WatchlistDelta push,
		CancellationToken cancellationToken
	)
	{
		var now = deps.Clock.Now;
		if (pulledAdds.Count > 0)
		{
			await deps.Publisher.PublishAsync(new WatchlistItemsAddedEvent(userId, now, pulledAdds), cancellationToken);
		}
		if (pulledRemoves.Count > 0)
		{
			await deps.Publisher.PublishAsync(new WatchlistItemsRemovedEvent(userId, now, pulledRemoves), cancellationToken);
		}
		if (pulledAdds.Count + pulledRemoves.Count > 0)
		{
			await deps.Publisher.PublishAsync(new WatchlistSyncedEvent(userId, now, pulledAdds, pulledRemoves, push.Adds, push.Removes), cancellationToken);
		}
	}

	private static async Task QueuePushesAsync(EngineDeps deps, WatchlistSyncPlan plan, UserId userId, CancellationToken cancellationToken)
	{
		await deps.Outbox.EnqueueAsync(WatchlistOperationAction.Add, plan.Push.Adds, cancellationToken);
		await deps.Outbox.EnqueueAsync(WatchlistOperationAction.Remove, plan.Push.Removes, cancellationToken);

		foreach (var mediaId in plan.Push.All)
		{
			await deps.Publisher.PublishAsync(new WatchlistPushRequest(userId, mediaId), cancellationToken);
		}
	}

	private WatchlistSyncResult OnPlanError(Error error, WatchlistProvider provider)
	{
		logger.LogWarning("Remote fetch failed for {Provider}: {Reason}", provider, error.Reason);
		return new WatchlistSyncRemoteFailed(error.Reason);
	}

	private static WatchlistSyncResult BuildChanges(WatchlistSyncPlan plan) => new WatchlistSyncSummary(plan.Pull, plan.Push, plan.NoOps);

	private static async Task<EngineDeps> ResolveAsync(AsyncServiceScope scope, CancellationToken cancellationToken)
	{
		var config = await scope.ServiceProvider.GetRequiredService<IProviderConfigRepository>().GetAsync(cancellationToken);
		var provider = config.WatchlistProvider;
		return new EngineDeps(
			Provider: provider,
			MediaProvider: provider.AsMediaProvider(),
			Repo: scope.ServiceProvider.GetRequiredService<IWatchlistRepository>(),
			Outbox: scope.ServiceProvider.GetRequiredService<IWatchlistOutbox>(),
			Media: scope.ServiceProvider.GetRequiredService<IMediaService>(),
			RemoteWatchlistProvider: scope.ServiceProvider.GetRequiredService<IKeyedServiceIndex<WatchlistProvider, IWatchlistProvider>>()[provider],
			Publisher: scope.ServiceProvider.GetRequiredService<IMessagePublisher>(),
			Clock: scope.ServiceProvider.GetRequiredService<IDateTimeProvider>()
		);
	}

	private async Task<Result<IReadOnlyList<MediaId>>> FetchRemoteAsync(EngineDeps deps, CancellationToken cancellationToken)
	{
		try
		{
			var externals = await deps.RemoteWatchlistProvider.GetWatchlistAsync(cancellationToken).ToListAsync(cancellationToken);
			var requests = externals.Select(e => new ResolveRequest([e], deps.MediaProvider, new None()));
			var remoteMediaIds = new List<MediaId>(externals.Count);
			var pipeline = deps.Media.IdentifyAsync(requests, cancellationToken).ZipAwait(externals, cancellationToken);
			await foreach (var (result, external) in pipeline)
			{
				if (result is Success<MediaIdentity>(var identity))
				{
					remoteMediaIds.Add(identity.Id);
				}
				else
				{
					logger.LogWarning("Remote watchlist item {External} could not be resolved during sync", external);
				}
			}
			return Success.Of<IReadOnlyList<MediaId>>(remoteMediaIds);
		}
		catch (Exception ex)
		{
			return Error.From(ex);
		}
	}

	private async Task<Result<WatchlistSyncPlan>> PlanAsync(EngineDeps deps, CancellationToken cancellationToken) =>
		await FetchRemoteAsync(deps, cancellationToken)
			.Map(async remoteIds =>
			{
				var localIds = await deps.Repo.GetIdsAsync(cancellationToken);
				var outboxOps = await deps.Outbox.GetAsync(cancellationToken);
				return Success.Of(WatchlistSyncPlan.Compute(remoteIds, localIds, outboxOps));
			});

	private readonly record struct EngineDeps(
		WatchlistProvider Provider,
		MediaProvider MediaProvider,
		IWatchlistRepository Repo,
		IWatchlistOutbox Outbox,
		IMediaService Media,
		IWatchlistProvider RemoteWatchlistProvider,
		IMessagePublisher Publisher,
		IDateTimeProvider Clock
	);
}
