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
}
