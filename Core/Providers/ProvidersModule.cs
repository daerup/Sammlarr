using Contracts.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace Core.Providers;

internal sealed class ProvidersModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddSingleton<IProviderRateLimitReporter, ProviderRateLimitReporter>();
		services.AddScoped<IServiceReadiness, ServiceReadiness>();
	}
}
