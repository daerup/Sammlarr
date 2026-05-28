namespace Contracts.Processing;

public interface IQueryProcessor<T>
{
	IAsyncEnumerable<QueryResult<T>> Execute(
		IEnumerable<T> source,
		QueryOptions options,
		Func<T, bool> isWatchlisted,
		Func<T, bool> isBlacklisted,
		CancellationToken cancellationToken = default
	);
}
