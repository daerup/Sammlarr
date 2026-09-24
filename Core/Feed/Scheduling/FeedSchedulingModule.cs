using Contracts.Feed;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace Core.Feed.Scheduling;

internal sealed class FeedSchedulingModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services) => services.AddScoped<IFeedScheduler, TickerFeedScheduler>();
}
