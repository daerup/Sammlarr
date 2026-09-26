using Contracts.Watchlist.Sync;
using Microsoft.Extensions.DependencyInjection;
using Observability.Tracing;
using Shared.DI;

namespace Core.Watchlist.Sync;

internal class SyncModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddSingleton<WatchlistSyncGate>();
		services.AddScoped<WatchlistSyncAnnouncer>();
		services.AddTracedScoped<IWatchlistSyncPlanner, WatchlistSyncPlanner>();
		services.AddTracedScoped<IWatchlistSyncEngine, WatchlistSyncEngine>();
	}
}
