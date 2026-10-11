using API.Http;
using Contracts.Datasets;
using Contracts.Media;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Shared.Options;

namespace API.Imdb;

internal class ImdbModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.ForRemote<ImdbDatasetsOptions>(RemoteService.ImdbDatasets).Client<ImdbRatingsFile>(Configure).Enricher<ImdbFileEnricher>();
		services.AddScoped<IDataset<ImdbRatingLine>>(provider => provider.GetRequiredService<ImdbRatingsFile>());
		services.AddScoped<ImdbRatingsInRun>();
		services.AddScoped<IBurstPrimer>(provider => provider.GetRequiredService<ImdbRatingsInRun>());
	}

	private static void Configure(IServiceProvider sp, HttpClient client)
	{
		var options = sp.GetRequiredService<ImdbDatasetsOptions>();
		client.BaseAddress = new Uri($"{options.BaseUrl.TrimEnd('/')}/");
		client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
	}
}
