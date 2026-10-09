using Contracts.Feed;
using Core.Feed.Trace;
using Microsoft.Extensions.DependencyInjection;
using Observability.Tracing;
using Shared.DI;

namespace Core.Feed;

internal class FeedModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<Scheduling.FeedSchedulingModule>();
		services.AddSingleton<FeedRunGate>();
		services.AddSingleton<FeedRunTrace>();
		services.AddSingleton<IFeedRunTrace>(provider => provider.GetRequiredService<FeedRunTrace>());
		services.AddScoped<IFeedRunTraces, FeedRunTraces>();
		services.AddTracedScoped<IFeedRunPlanner, FeedRunPlanner>();
		services.AddTracedScoped<IFeedRunEngine, FeedRunEngine>();
		services.AddTracedScoped<IFeedService, FeedService>();
	}
}
