using Contracts.Usage;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.Usage;

internal class UsageDataModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<IUsageStore, UsageStore>();
		services.AddScoped<IModelConfiguration, UsageConfiguration>();
	}
}
