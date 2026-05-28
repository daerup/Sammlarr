using Contracts.Auth;
using Contracts.Feed;
using Contracts.Media;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace API.Mdblist;

public class MdblistModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddHttpClient<MdblistClient>();
		services.AddKeyedScoped<IApiKeyValidator, MdblistClient>(MediaProvider.Mdblist);
		services.AddKeyedScoped<IFeedService, MdblistService>(FeedProvider.Mdblist);
	}
}
