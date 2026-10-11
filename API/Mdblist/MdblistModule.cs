using API.Http;
using Contracts.Usage;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Concurrency;
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
			.Enricher<MdblistEnricher>()
			.Converter<MdblistIdConverter>();
		services.AddScoped<MdblistTitles>();
		services.AddSingleton<MdblistBudget>();
		services.AddKeyedSingleton<ILaneQuota>(RemoteService.Mdblist, (provider, _) => provider.GetRequiredService<MdblistBudget>());
		services
			.AddHttpClient<MdblistClient>()
			.AddHttpMessageHandler(provider => new MdblistBudgetHandler(provider.GetRequiredService<MdblistBudget>()))
			.AddHttpMessageHandler(provider => new MdblistAnswerCheck(provider.GetRequiredService<ILogger<MdblistAnswerCheck>>()));
		services.AddScoped<IMdblistRequestsLeft, MdblistRequestsLeft>();
	}
}
