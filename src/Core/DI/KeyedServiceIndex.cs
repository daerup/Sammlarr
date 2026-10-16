using Contracts.DI;
using Microsoft.Extensions.DependencyInjection;

namespace Core.DI;

internal sealed class KeyedServiceIndex<TKey, TService>(IServiceProvider serviceProvider) : IKeyedServiceIndex<TKey, TService>
	where TKey : struct, Enum
	where TService : class
{
	public TService this[TKey key] => serviceProvider.GetRequiredKeyedService<TService>(key);
}
