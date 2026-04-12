using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Trending;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace API.TMDB;

internal class TmdbModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<TmdbApi>();
		services.AddHttpClient<TmdbClient>();
		services.AddKeyedScoped<IMediaRecommendationService, TmdbRecommendationService>(MediaRecommendationProvider.Tmdb);
		services.AddScoped<TmdbIdConverter>();
		services.AddKeyedScoped<IApiKeyValidator, TmdbClient>(MediaProvider.Tmdb);
	}
}
