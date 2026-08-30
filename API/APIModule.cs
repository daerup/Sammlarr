using API.Auth;
using API.Imdb;
using API.Mdblist;
using API.Notifications;
using API.Observability;
using API.Plex;
using API.Tmdb;
using API.TvDB;
using Contracts.Auth;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace API;

public class ApiModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<ImdbModule>();
		services.AddModule<TvdbModule>();
		services.AddModule<PlexModule>();
		services.AddModule<TmdbModule>();
		services.AddModule<MdblistModule>();
		services.AddModule<NotificationsApiModule>();
		services.AddModule<InstrumentationModule>();
		services.AddModule<ResilienceModule>();

		services.AddScoped<IApiKeyFactory, ApiKeyFactory>();
		services.AddScoped<ICredentialFactory, CredentialFactory>();
	}
}
