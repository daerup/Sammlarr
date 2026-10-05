using API.Http;
using Contracts.Media.Conversions;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Shared.Options;

namespace API.Wikidata;

internal class WikidataModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddHttpClient<WikidataIdConverter>(Configure).AddProviderHttp<WikidataOptions>(MediaProvider.RottenTomatoes);
		services.AddScoped<IExternalIdConverter>(sp => sp.GetRequiredService<WikidataIdConverter>());
	}

	private static void Configure(IServiceProvider sp, HttpClient client)
	{
		var options = sp.GetRequiredService<WikidataOptions>();
		client.BaseAddress = new Uri($"{options.BaseUrl.TrimEnd('/')}/");
		client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
	}
}
