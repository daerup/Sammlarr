using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace Shared.Caching;

internal class CachingModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddMemoryCache();
		services.AddScoped(typeof(ITypedMemoryCache<,>), typeof(TypedMemoryCache<,>));
	}
}
