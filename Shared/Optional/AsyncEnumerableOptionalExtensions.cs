namespace Shared;

public static class AsyncEnumerableOptionalExtensions
{
	public static async Task<Optional<T>> FirstOrNoneAsync<T>(
		this IAsyncEnumerable<T?> source,
		Func<T, bool> predicate,
		CancellationToken cancellationToken = default
	)
		where T : class
	{
		await foreach (var item in source.WithCancellation(cancellationToken))
		{
			if (item is not null && predicate(item))
			{
				return Some.Of(item);
			}
		}
		return new None();
	}
}
