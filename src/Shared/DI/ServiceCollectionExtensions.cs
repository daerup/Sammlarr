using Microsoft.Extensions.DependencyInjection;

namespace Shared.DI;

public static class ServiceCollectionExtensions
{
	extension(IServiceCollection services)
	{
		public void AddModule<TModule>()
			where TModule : IServiceModule, new() => new TModule().RegisterServices(services);

		public void AddModule<TModule>(TModule module)
			where TModule : IServiceModule => module.RegisterServices(services);
	}
}
