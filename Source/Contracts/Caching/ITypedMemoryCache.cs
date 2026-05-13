namespace Contracts.Caching;

public interface ITypedMemoryCache<in TKey, TValue>
	where TKey : notnull
{
	bool TryGetValue(TKey key, out TValue? value);

	void Set(TKey key, TValue value);

	void Set(TKey key, TValue value, TimeSpan absoluteExpirationRelativeToNow);

	void Remove(TKey key);
}
