using API.IMDB;
using API.Plex;
using API.TMDB;
using API.TvDB;
using Contracts.Media.Convertions;
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
		services.AddScoped<IMediaIdResolver, MediaIdResolver>();
	}
}
