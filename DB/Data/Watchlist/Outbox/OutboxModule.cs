using Contracts.Watchlist.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.Watchlist.Outbox;

internal class OutboxModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<IWatchlistOutbox, WatchlistOutboxRepository>();
		services.AddScoped<IModelConfiguration, WatchlistOutboxRowConfiguration>();
	}
}
