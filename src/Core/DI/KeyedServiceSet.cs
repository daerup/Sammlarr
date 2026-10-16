using Contracts.DI;

namespace Core.DI;

internal sealed class KeyedServiceSet<TKey, TService>(IEnumerable<Keyed<TKey, TService>> registered) : IKeyedServiceSet<TKey, TService>
	where TKey : struct, Enum
	where TService : class
{
	private readonly IReadOnlyDictionary<TKey, TService> _byKey = registered.ToDictionary(entry => entry.Key, entry => entry.Capability);

	public IReadOnlyCollection<TKey> Keys => [.. _byKey.Keys.Order()];

	public Optional<TService> Find(TKey key) => _byKey.GetValueOrNone(key);
}
