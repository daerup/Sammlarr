namespace Contracts.Caching;

public interface ITypedMemoryCache<in TKey, TValue>
	where TKey : notnull
	where TValue : notnull
{
	Optional<TValue> GetValueOrNone(TKey key);

	void Set(TKey key, TValue value);

	void Set(TKey key, TValue value, TimeSpan absoluteExpirationRelativeToNow);

	void Remove(TKey key);

	void Clear();
}
