using Contracts.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace Core.Caching;

internal sealed class TypedMemoryCache<TKey, TValue>(IMemoryCache cache) : ITypedMemoryCache<TKey, TValue>
	where TKey : notnull
	where TValue : notnull
{
	private static CancellationTokenSource clearSignal = new();

	public Optional<TValue> GetValueOrNone(TKey key)
	{
		return cache.TryGetValue(Namespaced(key), out var value) && value is TValue cached ? Some.Of(cached) : new None();
	}

	public void Set(TKey key, TValue value) => SetInternal(key, value, null);

	public void Set(TKey key, TValue value, TimeSpan absoluteExpirationRelativeToNow) => SetInternal(key, value, absoluteExpirationRelativeToNow);

	public void Remove(TKey key) => cache.Remove(Namespaced(key));

	public void Clear()
	{
		var previous = Interlocked.Exchange(ref clearSignal, new CancellationTokenSource());
		previous.Cancel();
		previous.Dispose();
	}

	private void SetInternal(TKey key, TValue value, TimeSpan? absoluteExpirationRelativeToNow)
	{
		var options = new MemoryCacheEntryOptions().AddExpirationToken(new CancellationChangeToken(clearSignal.Token));
		if (absoluteExpirationRelativeToNow is { } ttl)
		{
			options.AbsoluteExpirationRelativeToNow = ttl;
		}

		cache.Set(Namespaced(key), value, options);
	}

	private static (Type, Type, TKey) Namespaced(TKey key) => (typeof(TKey), typeof(TValue), key);
}
