using API.Http;
using Contracts.Datasets;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Shared.Options;

namespace API.Anime;

internal class AnimeListsModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.ForRemote<AnimeListsOptions>(RemoteService.AnimeLists).Client<AnimeListsFile>(Configure).Converter<AnimeListsIdConverter>();
		services.AddScoped<IDataset<AnimeIdMapping>>(provider => provider.GetRequiredService<AnimeListsFile>());
	}

	private static void Configure(IServiceProvider sp, HttpClient client)
	{
		var options = sp.GetRequiredService<AnimeListsOptions>();
		client.BaseAddress = new Uri($"{options.BaseUrl.TrimEnd('/')}/");
		client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
	}
}
