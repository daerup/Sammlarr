using System.ComponentModel;
using System.Linq.Dynamic.Core;
using Contracts.Processing;

namespace Core.Processing;

internal sealed class QueryProcessor<T> : IQueryProcessor<T>
{
	public IEnumerable<QueryResult<T>> Execute(IEnumerable<T> source, QueryOptions options)
	{
		ArgumentNullException.ThrowIfNull(source);

		var all = source.AsQueryable();

		if (options?.Sort is { } sort)
		{
			var dir = sort.Direction == ListSortDirection.Descending ? "descending" : "ascending";
			all = all.OrderBy($"{sort.Property} {dir}");
		}

		var filtered = options?.Filter is { } filter ? all.Where(FilterEvaluator.Build(filter)) : all;
		var removed = all.Except(filtered);
		var takeUntilIndex = options?.Limit is { } limit ? limit - 1 : int.MaxValue;

		foreach (var (index, item) in all.Index())
		{
			if (index > takeUntilIndex)
			{
				yield return new Skipped<T>(item);
			}
			else if (removed.Contains(item))
			{
				yield return new Filtered<T>(item);
			}
			else
			{
				yield return item;
			}
		}
	}
}
