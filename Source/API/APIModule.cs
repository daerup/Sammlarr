using API.IMDB;
using API.Mdblist;
using API.Plex;
using API.Telemetry;
using API.TMDB;
using API.TvDB;
using Contracts.Media.Convertions;
using Contracts.Media.Metadata;
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
		services.AddScoped<IMediaIdResolver, MediaIdResolver>();
		services.AddScoped<IMediaMetadataProvider, MediaMetadataProvider>();
	}
}
