using API.Http;
using Contracts.Media;
using Contracts.Media.Artwork;
using Contracts.Media.Conversions;
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
		services.AddSingleton<TmdbArtwork>();
		services.AddKeyedSingleton<IArtworkResizer, TmdbArtwork>(ArtworkProvider.Tmdb);
		services.AddHttpClient<TmdbClient>().AddProviderHttp<TmdbOptions>(MediaProvider.Tmdb);
		services.AddHttpClient(TmdbClient.Unauthenticated).AddProviderHttp<TmdbOptions>(MediaProvider.Tmdb);
		services.AddKeyedScoped<ITrendingService, TmdbTrendingService>(TrendingProvider.Tmdb);
		services.AddKeyedScoped<IDetailsEnricher, TmdbDetailsEnricher>(MediaProvider.Tmdb);
		services.AddScoped<IExternalIdConverter, TmdbIdConverter>();
	}
}
