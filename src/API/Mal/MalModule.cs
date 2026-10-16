using API.Http;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Shared.Options;

namespace API.Mal;

internal sealed class MalModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services
			.ForRemote<MalOptions>(RemoteService.Mal)
			.Client<MalClient>(Configure)
			.Client(MalClient.Unauthenticated)
			.ApiKey<MalClientIdCheck>(ApiKeyProvider.Mal)
			.Feed<MalFeedService>(FeedProvider.Mal)
			.Enricher<MalEnricher>();
	}

	private static void Configure(IServiceProvider sp, HttpClient client)
	{
		client.BaseAddress = new Uri($"{sp.GetRequiredService<MalOptions>().BaseUrl.TrimEnd('/')}/");
	}
}
