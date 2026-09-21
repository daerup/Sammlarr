using API.Http;
using Contracts.Feed;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Observability.Tracing;
using Shared.DI;
using Shared.Options;

namespace API.Mdblist;

public class MdblistModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddHttpClient<MdblistClient>().AddProviderHttp<MdblistOptions>(MediaProvider.Mdblist);
		services.AddHttpClient(MdblistClient.Unauthenticated).AddProviderHttp<MdblistOptions>(MediaProvider.Mdblist);
		services.AddTracedKeyedScoped<IFeedProvider, MdblistService>(FeedProvider.Mdblist);
	}
}
