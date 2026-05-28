using API.Imdb;
using API.Mdblist;
using API.Plex;
using API.Telemetry;
using API.Tmdb;
using API.TvDB;
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
		services.AddModule<TelemetryModule>();
	}
}
