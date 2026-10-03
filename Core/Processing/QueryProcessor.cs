using System.Collections;
using System.ComponentModel;
using System.Linq.Dynamic.Core;
using System.Runtime.CompilerServices;
using Domain.Processing;

namespace Core.Processing;

internal sealed class QueryProcessor<T>
	where T : class
{
	private static readonly ParsingConfig FilterConfig = new() { EvaluateGroupByAtDatabase = true, AllowEqualsAndToStringMethodsOnObject = true };

	private static readonly IEqualityComparer<T> Identity = ReferenceEqualityComparer.Instance;

	private CompiledFilter? _compiled;

	public async IAsyncEnumerable<QueryVerdict<T>> ExecuteAsync(
		IEnumerable<T> source,
		QueryOptions options,
		QueryPredicates<T> predicates,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		var all = options.Sort.Map(sort => Sort(source, sort)).GetValueOr(() => [.. source]);
		var qualifying = options.Filter.Map(filter => new HashSet<T>(all.Where(Compile(filter)), Identity));

		var taken = 0;
		const int cooperativeYieldInterval = 32;
		foreach (var (i, item) in all.Index())
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (i % cooperativeYieldInterval == 0)
			{
				await Task.Yield();
			}

			var enrollment = new Enrollment
			{
				InWantlist = predicates.IsWantlisted(item),
				InBlocklist = predicates.IsBlocklisted(item),
				Owned = predicates.IsOwned(item),
			};

			if (qualifying.Match(set => !set.Contains(item), () => false))
			{
				yield return new QueryVerdict<T>(item, QueryOutcome.Culled, enrollment, false);
			}
			else if (options.Limit.Match(limit => taken >= limit.Count, () => false))
			{
				yield return new QueryVerdict<T>(item, QueryOutcome.Truncated, enrollment, false);
			}
			else
			{
				var consumesSlot = options.Limit.Match(limit => ConsumesSlot(enrollment, limit), () => true);
				if (consumesSlot)
					taken++;
				yield return new QueryVerdict<T>(item, QueryOutcome.Qualified, enrollment, consumesSlot);
			}
		}
	}

	private static List<T> Sort(IEnumerable<T> source, QuerySort sort)
	{
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

	private static bool ConsumesSlot(Enrollment enrollment, QueryLimit limit)
	{
		if (enrollment.Owned)
		{
			return true;
		}

		if (enrollment.InWantlist)
		{
			return limit.Wantlisted is SlotPolicy.Consumed;
		}

		if (enrollment.InBlocklist)
		{
			return limit.Blocklisted is SlotPolicy.Consumed;
		}

		return true;
	}
}
