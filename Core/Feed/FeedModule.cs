using Contracts.Feed;
using Microsoft.Extensions.DependencyInjection;
using Observability.Tracing;
using Shared.DI;

namespace Core.Feed;

internal class FeedModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddSingleton<FeedRunGate>();
		services.AddTracedScoped<IFeedRunPlanner, FeedRunPlanner>();
		services.AddTracedScoped<IFeedRunEngine, FeedRunEngine>();
		services.AddTracedScoped<IFeedService, FeedService>();
	}
}
