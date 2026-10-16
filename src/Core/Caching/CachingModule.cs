using Contracts.Caching;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace Core.Caching;

internal sealed class CachingModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddMemoryCache();
		services.AddScoped(typeof(ITypedMemoryCache<,>), typeof(TypedMemoryCache<,>));
	}
}
