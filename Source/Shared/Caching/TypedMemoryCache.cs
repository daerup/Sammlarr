using Microsoft.Extensions.Caching.Memory;

namespace Shared.Caching;

public class TypedMemoryCache<TKey, TValue>(IMemoryCache cache) : ITypedMemoryCache<TKey, TValue>
	where TKey : notnull
{
	public bool TryGetValue(TKey key, out TValue? value) => cache.TryGetValue(key, out value);

	public void Set(TKey key, TValue value) => cache.Set(key, value);

	public void Remove(TKey key) => cache.Remove(key);
}
