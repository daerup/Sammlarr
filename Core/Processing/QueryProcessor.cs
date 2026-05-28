using System.ComponentModel;
using System.Linq.Dynamic.Core;
using System.Runtime.CompilerServices;
using Contracts.Processing;

namespace Core.Processing;

internal sealed class QueryProcessor<T> : IQueryProcessor<T>
{
	public async IAsyncEnumerable<QueryResult<T>> Execute(
		IEnumerable<T> source,
		QueryOptions options,
		Func<T, bool> isWatchlisted,
		Func<T, bool> isBlacklisted,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		var all = source.AsQueryable();

		if (options?.Sort is { } sort)
		{
			var dir = sort.Direction == ListSortDirection.Descending ? "descending" : "ascending";
			all = all.OrderBy($"{sort.Property} {dir}");
		}

        var config = ParsingConfig.DefaultEFCore21;
        config.AllowEqualsAndToStringMethodsOnObject = true;
		var filtered = options?.Filter is { } filter ? all.Where(config, FilterEvaluator.Build(filter)) : all;
		var removed = new HashSet<T>(all.Except(filtered));

		var taken = 0;
        const int cooperativeYieldInterval = 32;
		foreach (var (i, item) in all.Index())
		{
			cancellationToken.ThrowIfCancellationRequested();
            if (i % cooperativeYieldInterval == 0)
            {
				await Task.Yield();
            }

			if (removed.Contains(item))
			{
				yield return new Culled<T>(item);
			}
			else if (options?.Limit is { Count: var limit } && taken >= limit)
			{
				yield return new Truncated<T>(item);
			}
			else if (GetBypassReason(item, isWatchlisted, isBlacklisted) is { } enrollmentSource)
			{
				var consumesSlot = !SlotIsPreserved(enrollmentSource, options);
				if (consumesSlot)
					taken++;
				yield return new Bypassed<T>(item, enrollmentSource, consumesSlot);
			}
			else
			{
				taken++;
				yield return item;
			}
		}
	}

	private static BypassReason? GetBypassReason(T item, Func<T, bool> isWatchlisted, Func<T, bool> isBlacklisted)
	{
		return isWatchlisted(item) ? BypassReason.Watchlisted
			: isBlacklisted(item) ? BypassReason.Blacklisted
			: null;
	}

	private static bool SlotIsPreserved(BypassReason source, QueryOptions? options)
	{
		return options?.Limit is { } limit
			&& source switch
			{
				BypassReason.Watchlisted => limit.Watchlisted == SlotPolicy.Preserved,
				BypassReason.Blacklisted => limit.Blacklisted == SlotPolicy.Preserved,
				_ => false,
			};
	}
}
