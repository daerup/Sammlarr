using Contracts.Auth;
using Contracts.Feed;
using Contracts.Media;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Observability.Tracing;
using Shared.DI;

namespace API.Mdblist;

public class MdblistModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddHttpClient<MdblistClient>();
		services.AddHttpClient(MdblistClient.Unauthenticated);
		services.AddTracedKeyedScoped<IFeedService, MdblistService>(FeedProvider.Mdblist);
	}
}
