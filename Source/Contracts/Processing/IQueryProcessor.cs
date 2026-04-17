namespace Contracts.Processing;

public interface IQueryProcessor<T>
{
	IEnumerable<QueryResult<T>> Execute(IEnumerable<T> source, QueryOptions options);
}
