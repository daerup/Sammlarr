using Contracts;
using Contracts.Media.Blacklist;
using Contracts.Processing;
using Contracts.Watchlist;
using Core.Blacklist;
using Core.Caching;
using Core.DI;
using Core.Processing;
using Core.Watchlist;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace Core;

public class CoreModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<CachingModule>();
		services.AddModule<DiModule>();
		services.AddTransient<IDateTimeProvider, SystemDateTimeProvider>();
		services.AddTransient(typeof(IQueryProcessor<>), typeof(QueryProcessor<>));
		services.AddScoped<IBlacklistService, BlacklistService>();
		services.AddScoped<IWatchlistService, WatchlistService>();
	}
}
