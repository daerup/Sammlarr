namespace Contracts.Processing;

public interface IQueryProcessor<T>
	where T : class
{
	IAsyncEnumerable<QueryResult<T>> ExecuteAsync(
		IEnumerable<T> source,
		QueryOptions options,
		Func<T, bool> isWatchlisted,
		Func<T, bool> isBlacklisted,
		CancellationToken cancellationToken = default
	);
}
