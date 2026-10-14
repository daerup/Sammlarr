using Contracts.Settings;
using Contracts.Setup;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.Config;

internal sealed class ConfigModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<IProviderConfigRepository, ProviderConfigRepository>();
		services.AddScoped<IArrSinkConfigRepository, ArrSinkConfigRepository>();
		services.AddScoped<ISetupStateRepository, SetupStateRepository>();
		services.AddScoped<IModelConfiguration, ProviderConfigConfiguration>();
	}
}
