using Contracts.Housekeeping;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace Core.Housekeeping;

internal sealed class HousekeepingModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<HousekeepingPass>();
		services.AddScoped<IHousekeepingTasks, HousekeepingTasks>();
		services.AddScoped<IHousekeepingStep, UsageTrim>();
	}
}
