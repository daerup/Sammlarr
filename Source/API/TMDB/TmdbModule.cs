using Contracts.Media;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace API.TMDB;

public class TmdbModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<TmdbApi>();
		services.AddHttpClient<TmdbClient>();
		services.AddKeyedScoped<IMediaRecommendationService, TmdbApi>(MediaProvider.Tmdb);
		services.AddKeyedScoped<IMediaIdConverter, TmdbIdConverter>(MediaProvider.Tmdb);
	}
}
