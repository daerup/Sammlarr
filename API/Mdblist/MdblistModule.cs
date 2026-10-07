using API.Http;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Shared.Options;

namespace API.Mdblist;

public class MdblistModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services
			.ForRemote<MdblistOptions>(RemoteService.Mdblist)
			.Client<MdblistClient>()
			.Client(MdblistClient.Unauthenticated)
			.ApiKey<MdblistKeyCheck>(ApiKeyProvider.Mdblist)
			.Feed<MdblistService>(FeedProvider.Mdblist)
			.Enricher<MdblistEnricher>();
		services.AddScoped<MdblistTitles>();
		services.AddSingleton<MdblistBudget>();
	}
}
