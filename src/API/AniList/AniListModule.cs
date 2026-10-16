using API.Anime;
using API.Http;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Shared.Options;

namespace API.AniList;

internal sealed class AniListModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services
			.ForRemote<AniListOptions>(RemoteService.AniList)
			.Client<AniListClient>(Configure)
			.Feed<AniListFeedService>(FeedProvider.AniList)
			.Enricher<AniListEnricher>();
		services.AddScoped<AniListTitles>();
		services.AddScoped<IAnimePrequels, AniListPrequels>();
	}

	private static void Configure(IServiceProvider sp, HttpClient client)
	{
		var options = sp.GetRequiredService<AniListOptions>();
		client.BaseAddress = new Uri($"{options.BaseUrl.TrimEnd('/')}/");
		client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
	}
}
