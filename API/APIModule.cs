using API.Auth;
using API.Mdblist;
using API.Notifications;
using API.Observability;
using API.Plex;
using API.Tmdb;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace API;

public class ApiModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<AuthModule>();
		services.AddModule<PlexModule>();
		services.AddModule<TmdbModule>();
		services.AddModule<MdblistModule>();
		services.AddModule<NotificationsApiModule>();
		services.AddModule<InstrumentationModule>();
		services.AddModule<ResilienceModule>();
	}
}
