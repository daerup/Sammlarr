using API.Http;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Shared.Options;

namespace API.Trakt;

internal class TraktModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services
			.ForRemote<TraktOptions>(RemoteService.Trakt)
			.Client<TraktClient>(Configure)
			.Client(TraktClient.Unauthenticated)
			.ApiKey<TraktClientIdCheck>(ApiKeyProvider.Trakt)
			.Feed<TraktFeedService>(FeedProvider.Trakt)
			.Converter<TraktIdConverter>()
			.Enricher<TraktEnricher>();
		services.AddScoped<TraktTitles>();
	}

	private static void Configure(IServiceProvider sp, HttpClient client)
	{
		var options = sp.GetRequiredService<TraktOptions>();
		client.BaseAddress = new Uri($"{options.BaseUrl.TrimEnd('/')}/");
		client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
	}
}
