using Contracts.Watchlist;
using DB.Data.Watchlist.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.Watchlist;

internal class WatchlistModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<OutboxModule>();
		services.AddScoped<IWatchlistRepository, WatchlistRepository>();
		services.AddScoped<IModelConfiguration, WatchlistRowConfiguration>();
	}
}
