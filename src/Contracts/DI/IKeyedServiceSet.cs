namespace Contracts.DI;

public interface IKeyedServiceSet<TKey, TService>
	where TKey : struct, Enum
	where TService : class
{
	IReadOnlyCollection<TKey> Keys { get; }

	Optional<TService> Find(TKey key);
}
