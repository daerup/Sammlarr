using Contracts.DI;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace Core.DI;

internal sealed class DiModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped(typeof(IKeyedServiceIndex<,>), typeof(KeyedServiceIndex<,>));
		services.AddScoped(typeof(IKeyedServiceSet<,>), typeof(KeyedServiceSet<,>));
	}
}
