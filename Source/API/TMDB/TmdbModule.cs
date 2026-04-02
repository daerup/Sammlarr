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
		services.AddKeyedScoped<IMediaRecommendationService, TmdbApi>(MediaProvider.Tmdb);
		services.AddKeyedScoped<IMediaIdConverter, TmdbIdConverter>(MediaProvider.Tmdb);
		// Expose TmdbClient through the IApiKeyValidator contract so UI does not hard-reference the concrete type.
		services.AddKeyedScoped<IApiKeyValidator>(MediaProvider.Tmdb, (sp, _) => sp.GetRequiredService<TmdbClient>());
	}
}
