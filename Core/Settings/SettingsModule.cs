using Contracts.Settings;
using Microsoft.Extensions.DependencyInjection;
using Observability.Tracing;
using Shared.DI;

namespace Core.Settings;

internal sealed class SettingsModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddTracedScoped<IProviderConfigService, ProviderConfigService>();
		services.AddTracedScoped<IArrSinkConfigService, ArrSinkConfigService>();
	}
}
