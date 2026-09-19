using Domain.Processing;

namespace Contracts.Processing;

public interface IQueryProcessor<T>
	where T : class
{
	IAsyncEnumerable<QueryVerdict<T>> ExecuteAsync(
		IEnumerable<T> source,
		QueryOptions options,
		QueryPredicates<T> predicates,
		CancellationToken cancellationToken = default
	);
}
