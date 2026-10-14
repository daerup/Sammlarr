using Contracts.DI;

namespace TestInfrastructure;

public class FakeKeyedServiceIndex<TKey, TService> : IKeyedServiceIndex<TKey, TService>
	where TKey : struct, Enum
	where TService : class
{
	private readonly Dictionary<TKey, TService> _services = [];

	public TService this[TKey key] =>
		_services.TryGetValue(key, out var service) ? service : throw new InvalidOperationException($"No service registered for key '{key}'.");

	public FakeKeyedServiceIndex<TKey, TService> Register(TKey key, TService service)
	{
		_services[key] = service;
		return this;
	}
}
