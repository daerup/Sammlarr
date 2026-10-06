using API.Http;
using Contracts.Media.Artwork;
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
		services.AddSingleton<IArtworkResizer>(provider => provider.GetRequiredService<TmdbArtwork>());
		services
			.ForRemote<TmdbOptions>(RemoteService.Tmdb)
			.Client<TmdbClient>()
			.Client(TmdbClient.Unauthenticated)
			.ApiKey<TmdbKeyCheck>(ApiKeyProvider.Tmdb)
			.Trending<TmdbTrendingService>(TrendingProvider.Tmdb)
			.Search<TmdbSearch>(SearchProvider.Tmdb)
			.Converter<TmdbIdConverter>()
			.Enricher<TmdbDetailsEnricher>();
	}
}
