using Contracts.Feed;
using Microsoft.Extensions.DependencyInjection;
using Observability.Tracing;
using Shared.DI;

namespace Core.Feed.Scheduling;

internal sealed class FeedSchedulingModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services) => services.AddTracedScoped<IFeedScheduler, TickerFeedScheduler>();
}
