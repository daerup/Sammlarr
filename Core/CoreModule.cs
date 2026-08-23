using Contracts;
using Contracts.Auth;
using Contracts.Blacklist;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Media.Search;
using Contracts.Processing;
using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Contracts.Watchlist.Sync;
using Core.Auth;
using Core.Blacklist;
using Core.Caching;
using Core.DI;
using Core.Identity;
using Core.Media;
using Core.Processing;
using Core.Watchlist;
using Core.Watchlist.Sync;
using Microsoft.Extensions.DependencyInjection;
using Observability.Tracing;
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
		services.AddTracedScoped<IBlacklistService, BlacklistService>();
		services.AddTracedScoped<IWatchlistService, WatchlistService>();
		services.AddSingleton<IWatchlistSyncEngine, WatchlistSyncEngine>();
		services.AddSingleton<IWatchlistOutboxEventFeed, WatchlistOutboxEventFeed>();
		services.AddScoped<MediaIdResolver>();
		services.AddScoped<IUnresolvedMediaCache, UnresolvedMediaCache>();
		services.AddScoped<MediaMetadataResolver>();
		services.AddScoped<MediaIdentityCoordinator>();
		services.AddScoped<MediaResolutionBufferFactory>();
		services.AddTracedScoped<IMediaService, MediaService>();
		services.AddTracedScoped<IMediaSearchService, MediaSearchService>();
		services.AddScoped<CurrentUserOverride>();
		services.AddScoped<IUserScope, UserScope>();
		services.AddTracedScoped<IUserManagementService, UserManagementService>();
		services.AddTracedScoped<ISignInService, SignInService>();
		services.AddScoped<ISessionValidator, SessionValidator>();
	}
}
