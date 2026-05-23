using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using DB.Data.Watchlist.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.Watchlist;

internal class WatchlistModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<IWatchlistRepository, WatchlistRepository>();
		services.AddScoped<IWatchlistOutbox, WatchlistOutboxRepository>();
		services.AddScoped<IModelConfiguration, WatchlistEntityConfiguration>();
		services.AddScoped<IModelConfiguration, WatchlistOutboxEntityConfiguration>();
	}
}
