using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Core.Watchlist.Sync;
using Microsoft.Extensions.DependencyInjection;
using Observability.Tracing;
using Shared.DI;

namespace Core.Watchlist;

internal class WatchlistModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<SyncModule>();
		services.AddTracedScoped<IWatchlistService, WatchlistService>();
		services.AddSingleton<IWatchlistOutboxEventFeed, WatchlistOutboxEventFeed>();
	}
}
