using Contracts.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace API.Http;

internal class HttpModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddSingleton<ProviderConcurrencyGates>();
		services.AddSingleton<ProviderCapacity>();
		services.AddSingleton<IProviderCapacity>(sp => sp.GetRequiredService<ProviderCapacity>());
	}
}
