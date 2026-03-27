namespace Shared.DI;

public interface IKeyedServiceIndex<in TKey, out TService>
	where TKey : struct, Enum
	where TService : class
{
	TService this[TKey key] { get; }
}
