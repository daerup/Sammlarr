using System.Net;
using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Convertions;
using Contracts.Media.Metadata;
using Contracts.Media.Trending;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace API.Tmdb;

internal class TmdbModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<TmdbApi>();
		services.AddHttpClient<TmdbClient>(client =>
		{
			client.DefaultRequestVersion = HttpVersion.Version20;
			client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
		});
		services.AddKeyedScoped<IMediaRecommendationService, TmdbRecommendationService>(MediaRecommendationProvider.Tmdb);
		services.AddKeyedScoped<IMetadataEnricher, TmdbMetadataEnricher>(MediaProvider.Tmdb);
		services.AddKeyedScoped<IMediaRatingService, TmdbRatingService>(RatingProvider.Tmdb);
		services.AddKeyedScoped<IExternalIdConverter, TmdbIdConverter>(MediaProvider.Tmdb);
		services.AddKeyedScoped<IApiKeyValidator, TmdbClient>(MediaProvider.Tmdb);
	}
}
