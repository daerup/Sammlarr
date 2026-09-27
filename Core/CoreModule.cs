using Contracts;
using Core.Announcing;
using Core.Auth;
using Core.Blacklist;
using Core.Caching;
using Core.DI;
using Core.Feed;
using Core.Identity;
using Core.Jobs;
using Core.Media;
using Core.Notifications;
using Core.Processing;
using Core.Providers;
using Core.Watchlist;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace Core;

public class CoreModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<AnnouncingModule>();
		services.AddModule<AuthModule>();
		services.AddModule<BlacklistModule>();
		services.AddModule<CachingModule>();
		services.AddModule<DiModule>();
		services.AddModule<FeedModule>();
		services.AddModule<IdentityModule>();
		services.AddModule<JobsModule>();
		services.AddModule<MediaModule>();
		services.AddModule<NotificationsModule>();
		services.AddModule<ProcessingModule>();
		services.AddModule<ProvidersModule>();
		services.AddModule<WatchlistModule>();
		services.AddTransient<IDateTimeProvider, SystemDateTimeProvider>();
	}
}
