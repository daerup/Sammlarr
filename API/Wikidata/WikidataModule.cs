using API.Http;
using Contracts.Ratings;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Shared.Options;

namespace API.Wikidata;

internal class WikidataModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddHttpClient<IRottenTomatoesPageFinder, WikidataClient>(Configure).AddProviderHttp<WikidataOptions>();
	}

	private static void Configure(IServiceProvider sp, HttpClient client)
	{
		var options = sp.GetRequiredService<WikidataOptions>();
		client.BaseAddress = new Uri($"{options.BaseUrl.TrimEnd('/')}/");
		client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
	}
}
