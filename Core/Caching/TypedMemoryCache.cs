using Contracts.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace Core.Caching;

internal class TypedMemoryCache<TKey, TValue>(IMemoryCache cache) : ITypedMemoryCache<TKey, TValue>
	where TKey : notnull
{
	public bool TryGetValue(TKey key, out TValue? value) => cache.TryGetValue(Namespaced(key), out value);

	public void Set(TKey key, TValue value) => cache.Set(Namespaced(key), value);

	public void Set(TKey key, TValue value, TimeSpan absoluteExpirationRelativeToNow) => cache.Set(Namespaced(key), value, absoluteExpirationRelativeToNow);

	public void Remove(TKey key) => cache.Remove(Namespaced(key));

	private static (Type, Type, TKey) Namespaced(TKey key) => (typeof(TKey), typeof(TValue), key);
}
