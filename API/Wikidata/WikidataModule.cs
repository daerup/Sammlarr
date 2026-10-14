using API.Http;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Shared.Options;

namespace API.Wikidata;

internal sealed class WikidataModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.ForRemote<WikidataOptions>(RemoteService.Wikidata).Client<WikidataIdConverter>(Configure).Converter<WikidataIdConverter>();
	}

	private static void Configure(IServiceProvider sp, HttpClient client)
	{
		var options = sp.GetRequiredService<WikidataOptions>();
		client.BaseAddress = new Uri($"{options.BaseUrl.TrimEnd('/')}/");
		client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
	}
}
