using API.Plex;
using API.TMDB;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace API;

public class ApiModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<PlexModule>();
		services.AddModule<TmdbModule>();
	}
}
