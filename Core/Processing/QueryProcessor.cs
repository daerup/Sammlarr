using System.Collections;
using System.ComponentModel;
using System.Linq.Dynamic.Core;
using System.Runtime.CompilerServices;
using Contracts.Processing;
using Domain.Processing;

namespace Core.Processing;

internal sealed class QueryProcessor<T> : IQueryProcessor<T>
	where T : class
{
	private static readonly ParsingConfig FilterConfig = new() { EvaluateGroupByAtDatabase = true, AllowEqualsAndToStringMethodsOnObject = true };

	private static readonly IEqualityComparer<T> Identity = ReferenceEqualityComparer.Instance;

	private CompiledFilter? _compiled;

	public async IAsyncEnumerable<QueryResult<T>> ExecuteAsync(
		IEnumerable<T> source,
		QueryOptions options,
		Func<T, bool> isWatchlisted,
		Func<T, bool> isBlacklisted,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		var all = Sort(source, options?.Sort);
		var qualifying = options?.Filter is { } filter ? new HashSet<T>(all.Where(Compile(filter)), Identity) : null;

		var taken = 0;
		const int cooperativeYieldInterval = 32;
		foreach (var (i, item) in all.Index())
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (i % cooperativeYieldInterval == 0)
			{
				await Task.Yield();
			}

			if (qualifying?.Contains(item) is false)
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

	private static List<T> Sort(IEnumerable<T> source, QuerySort? sort)
	{
		if (sort is null)
		{
			return [.. source];
		}

		var direction = sort.Direction switch
		{
			ListSortDirection.Ascending => "ascending",
			ListSortDirection.Descending => "descending",
		};
		return [.. source.AsQueryable().OrderBy($"{sort.Property} {direction}")];
	}

	private Func<T, bool> Compile(QueryFilter filter)
	{
		var expression = FilterEvaluator.Build(filter);
		var compiled = _compiled;
		if (compiled is null || !string.Equals(compiled.Expression, expression, StringComparison.Ordinal))
		{
			compiled = new CompiledFilter(expression, DynamicExpressionParser.ParseLambda<T, bool>(FilterConfig, false, expression).Compile());
			_compiled = compiled;
		}

		return compiled.Predicate;
	}

	private sealed record CompiledFilter(string Expression, Func<T, bool> Predicate);

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
