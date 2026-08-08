using API.Http;
using Contracts.Auth;
using Contracts.Feed;
using Contracts.Media;
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
		services.AddHttpClient<MdblistClient>().AddProviderHttp<MdblistOptions>();
		services.AddHttpClient(MdblistClient.Unauthenticated).AddProviderHttp<MdblistOptions>();
		services.AddTracedKeyedScoped<IFeedService, MdblistService>(FeedProvider.Mdblist);
	}
}
