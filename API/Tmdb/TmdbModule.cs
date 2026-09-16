using API.Http;
using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Conversions;
using Contracts.Media.Metadata;
using Contracts.Media.Trending;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Shared.Options;

namespace API.Tmdb;

internal class TmdbModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<TmdbApi>();
		services.AddHttpClient<TmdbClient>().AddProviderHttp<TmdbOptions>(MediaProvider.Tmdb);
		services.AddHttpClient(TmdbClient.Unauthenticated).AddProviderHttp<TmdbOptions>(MediaProvider.Tmdb);
		services.AddKeyedScoped<ITrendingService, TmdbTrendingService>(TrendingProvider.Tmdb);
		services.AddKeyedScoped<IMetadataEnricher, TmdbMetadataEnricher>(MediaProvider.Tmdb);
		services.AddKeyedScoped<IMediaRatingService, TmdbRatingService>(RatingProvider.Tmdb);
		services.AddKeyedScoped<IExternalIdConverter, TmdbIdConverter>(MediaProvider.Tmdb);
	}
}
