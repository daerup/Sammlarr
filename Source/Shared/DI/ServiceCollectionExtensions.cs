using Microsoft.Extensions.DependencyInjection;

namespace Shared.DI;

public static class ServiceCollectionExtensions
{
	public static void AddModule<TModule>(this IServiceCollection services)
		where TModule : IServiceModule, new()
	{
		new TModule().RegisterServices(services);
	}
}
