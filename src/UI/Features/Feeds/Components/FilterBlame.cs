using Domain.Processing;

namespace UI.Features.Feeds.Components;

internal sealed record FilterBlame(FilterGroup Root, IReadOnlyList<FilterPredicate> Rules, IReadOnlyList<FilterGroup> Groups)
{
	public static FilterBlame Of(QueryFilter filter, FilterChecks checks)
	{
		var root = filter.Groups.Count == 1 ? filter.Groups[0] : new FilterGroup { Operator = filter.Operator, Groups = filter.Groups };
		return new FilterBlame(root, [.. BlamedRules(root, checks)], [.. BlamedGroups(root, checks)]);
	}

	public int Places => Rules.Count + Groups.Count;

	public bool Blames(FilterGroup group) => Groups.Contains(group, ReferenceEqualityComparer.Instance);

	public bool BlamesARuleIn(FilterGroup group) => group.Predicates.Any(Rules.Contains);

	public bool Leads(FilterGroup group) => Blames(group) || BlamesARuleIn(group) || group.Groups.Any(Leads);

	private static IEnumerable<FilterPredicate> BlamedRules(FilterGroup group, FilterChecks checks)
	{
		return group.Operator switch
		{
			FilterOperator.And => group.Predicates.Where(checks.Fails).Concat(Failing(group, checks).SelectMany(nested => BlamedRules(nested, checks))),
			FilterOperator.Or => [],
		};
	}

	private static IEnumerable<FilterGroup> BlamedGroups(FilterGroup group, FilterChecks checks)
	{
		return group.Operator switch
		{
			FilterOperator.And => Failing(group, checks).SelectMany(nested => BlamedGroups(nested, checks)),
			FilterOperator.Or => [group],
		};
	}

	private static IEnumerable<FilterGroup> Failing(FilterGroup group, FilterChecks checks) => group.Groups.Where(checks.Fails);
}
