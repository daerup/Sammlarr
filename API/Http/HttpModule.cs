using Contracts.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace API.Http;

internal class HttpModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddSingleton<ProviderGates>();
		services.AddSingleton<RemoteCapacity>();
		services.AddSingleton<IRemoteCapacity>(sp => sp.GetRequiredService<RemoteCapacity>());
	}
}
