using System.ComponentModel;
using System.Linq.Dynamic.Core;
using System.Runtime.CompilerServices;
using Domain.Processing;

namespace Core.Processing;

internal sealed class QueryProcessor<T>
	where T : class
{
	private static readonly ParsingConfig FilterConfig = new() { EvaluateGroupByAtDatabase = true, AllowEqualsAndToStringMethodsOnObject = true };

	private static readonly IEqualityComparer<T> ByReference = ReferenceEqualityComparer.Instance;

	private CompiledFilter? _compiled;

	public async IAsyncEnumerable<QueryVerdict<T>> ExecuteAsync(
		IEnumerable<T> source,
		QueryOptions options,
		QueryPredicates<T> predicates,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		var all = options.Sort.Map(sort => Sort(source, sort)).GetValueOr(() => [.. source]);
		var qualifying = options.Filter.Map(filter => new HashSet<T>(all.Where(Compile(FilterValues.Resolve(filter, options.Today))), ByReference));
		var verdicts = new QueryVerdict<T>[all.Count];
		var checks = new Lazy<RuleChecks>(() => new RuleChecks(options.Filter.Map(filter => Checks(filter, options.Today)).GetValueOr(() => [])));
		var line = options.Limit.Map(limit => new SlotLine(limit));

		const int cooperativeYieldInterval = 32;
		foreach (var (i, position) in Slotted(all, options, predicates).Index())
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (i % cooperativeYieldInterval == 0)
			{
				await Task.Yield();
			}

			var item = all[position];
			var enrollment = new Enrollment
			{
				InWantlist = predicates.IsWantlisted(item),
				InBlocklist = predicates.IsBlocklisted(item),
				Owned = predicates.IsOwned(item),
			};

			if (qualifying.Match(set => !set.Contains(item), () => false))
			{
				verdicts[position] = new QueryVerdict<T>(item, enrollment, new Culled(checks.Value.Of(item)));
			}
			else if (enrollment.Owned && SitsOutTheDraw(options))
			{
				verdicts[position] = new QueryVerdict<T>(item, enrollment, new Qualified(ConsumesSlot: false, Rank: new None()));
			}
			else
			{
				verdicts[position] = new QueryVerdict<T>(
					item,
					enrollment,
					line.Match(slots => slots.Place(enrollment), () => new Qualified(ConsumesSlot: true, Rank: new None()))
				);
			}
		}

		foreach (var verdict in verdicts)
		{
			yield return verdict;
		}
	}

	private static IEnumerable<int> Slotted(List<T> all, QueryOptions options, QueryPredicates<T> predicates)
	{
		var positions = Enumerable.Range(0, all.Count);
		if (!PicksAtRandom(options))
		{
			return positions;
		}

		return positions
			.OrderBy(position => ShuffleRank.Of(options.ShuffleSeed, predicates.Identity(all[position])))
			.ThenBy(position => predicates.Identity(all[position]));
	}

	private static bool PicksAtRandom(QueryOptions options) => options.Limit.Match(limit => limit.Pick is LimitPick.Random, () => false);

	private static bool SitsOutTheDraw(QueryOptions options) => options.DrawSkipsOwned && PicksAtRandom(options);

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
		if (compiled is null || !compiled.Expression.Equals(expression))
		{
			compiled = new CompiledFilter(expression, Lambda(expression));
			_compiled = compiled;
		}

		return compiled.Predicate;
	}

	private static Func<T, bool> Lambda(FilterExpression expression)
	{
		return DynamicExpressionParser.ParseLambda<T, bool>(FilterConfig, false, expression.Text, [.. expression.Values]).Compile();
	}

	private static IReadOnlyList<RuleCheck> Checks(QueryFilter filter, DateOnly today)
	{
		return
		[
			.. filter
				.Groups.SelectMany(Rules)
				.Distinct()
				.Select(written => FilterEvaluator.Predicate(FilterValues.Resolve(written, today)).Map(expression => new RuleCheck(written, Lambda(expression))))
				.WhereSome(),
		];
	}

	private static IEnumerable<FilterPredicate> Rules(FilterGroup group) => group.Predicates.Concat(group.Groups.SelectMany(Rules));

	private sealed record CompiledFilter(FilterExpression Expression, Func<T, bool> Predicate);

	private sealed record RuleCheck(FilterPredicate Written, Func<T, bool> Passes);

	private sealed class RuleChecks(IReadOnlyList<RuleCheck> rules)
	{
		private readonly ValueSet<FilterPredicate> _checked = [.. rules.Select(rule => rule.Written)];

		public FilterChecks Of(T item)
		{
			return new FilterChecks(_checked, [.. rules.Where(rule => !rule.Passes(item)).Select(rule => rule.Written)]);
		}
	}

	private sealed class SlotLine(QueryLimit limit)
	{
		private int _inLine;

		public QueryOutcome Place(Enrollment enrollment)
		{
			var rank = new SlotRank(_inLine + 1, limit.Count);
			var takesSlot = ConsumesSlot(enrollment, limit);
			if (takesSlot)
			{
				_inLine++;
			}

			return rank.Position > limit.Count ? new Truncated(rank) : new Qualified(takesSlot, Optional.When(takesSlot, () => rank));
		}
	}

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
