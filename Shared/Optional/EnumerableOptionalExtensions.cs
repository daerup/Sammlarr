namespace Shared;

public static class EnumerableOptionalExtensions
{
	public static async Task<Optional<T>> FirstSomeAsync<TSource, T>(this IEnumerable<TSource> source, Func<TSource, Task<Optional<T>>> selector)
		where T : notnull
	{
		foreach (var item in source)
		{
			if (await selector(item) is Some<T> some)
			{
				return some;
			}
		}
		return new None();
	}

	public static Optional<T> FirstOrNone<T>(this IEnumerable<T> source)
		where T : notnull
	{
		foreach (var item in source)
		{
			return Some.Of(item);
		}

		return new None();
	}

	public static Optional<T> FirstOrNone<T>(this IEnumerable<T> source, Func<T, bool> predicate)
		where T : notnull
	{
		foreach (var item in source)
		{
			if (predicate(item))
			{
				return Some.Of(item);
			}
		}

		return new None();
	}

	public static Optional<TValue> GetValueOrNone<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> source, TKey key)
		where TValue : notnull
	{
		return source.TryGetValue(key, out var value) ? Some.Of(value) : new None();
	}

	public static IEnumerable<T> WhereSome<T>(this IEnumerable<Optional<T>> source)
		where T : notnull
	{
		foreach (var item in source)
		{
			if (item is Some<T>(var value))
			{
				yield return value;
			}
		}
	}
}
