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

		var taken = 0;
		foreach (var item in all)
		{
			if (removed.Contains(item))
			{
				yield return new Filtered<T>(item);
			}
			else if (options?.Limit is { Count: var limit } && taken >= limit)
			{
				yield return new Skipped<T>(item);
			}
			else
			{
				taken++;
				yield return item;
			}
		}
	}
}
