using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.Scheduler;

internal class SchedulerDataModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<IModelConfiguration, TickerConfiguration>();
	}
}
