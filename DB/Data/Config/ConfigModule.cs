using Contracts.Settings;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.Config;

internal class ConfigModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<IProviderConfigRepository, ProviderConfigRepository>();
		services.AddScoped<IArrSinkConfigRepository, ArrSinkConfigRepository>();
		services.AddScoped<IModelConfiguration, ProviderConfigConfiguration>();
	}
}
