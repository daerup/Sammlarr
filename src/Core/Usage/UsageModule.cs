using Contracts.Usage;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace Core.Usage;

internal sealed class UsageModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddSingleton<IUsageLastCalls, UsageLastCalls>();
		services.AddSingleton<IFeedRunUsage, FeedRunUsage>();
		services.AddScoped<UsageFeedLines>();
		services.AddScoped<IUsageReport, UsageReport>();
	}
}
