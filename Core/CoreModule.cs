using Contracts;
using Contracts.Auth;
using Contracts.Blacklist;
using Contracts.Media;
using Contracts.Media.Search;
using Contracts.Processing;
using Contracts.Watchlist;
using Contracts.Watchlist.Sync;
using Core.Auth;
using Core.Blacklist;
using Core.Caching;
using Core.DI;
using Core.Media;
using Core.Processing;
using Core.Watchlist;
using Core.Watchlist.Sync;
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
		services.AddSingleton<IWatchlistSyncEngine, WatchlistSyncEngine>();
		services.AddScoped<MediaIdResolver>();
		services.AddScoped<MediaMetadataResolver>();
		services.AddScoped<MediaIdentityCoordinator>();
		services.AddScoped<IMediaService, MediaService>();
		services.AddScoped<IMediaSearchService, MediaSearchService>();
		services.AddScoped<IOAuthService, OAuthService>();
	}
}
