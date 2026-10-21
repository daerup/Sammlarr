namespace Domain.Processing;

public sealed record FilterChecks(ValueSet<FilterPredicate> Checked, ValueSet<FilterPredicate> Failed)
{
	public bool Counts(FilterPredicate rule) => Checked.Contains(rule);

	public bool Counts(FilterGroup group) => group.Predicates.Any(Counts) || group.Groups.Any(Counts);

	public bool Fails(FilterPredicate rule) => Failed.Contains(rule);

	public bool Fails(QueryFilter filter) => filter.Groups.Any(Counts) && Fails(filter.Operator, [], filter.Groups);

	public bool Fails(FilterGroup group) => Counts(group) && Fails(group.Operator, group.Predicates, group.Groups);

	private bool Fails(FilterOperator @operator, IReadOnlyList<FilterPredicate> predicates, IReadOnlyList<FilterGroup> groups)
	{
		var rules = predicates.Where(rule => Counts(rule)).ToList();
		var nested = groups.Where(group => Counts(group)).ToList();
		return @operator switch
		{
			FilterOperator.And => rules.Any(rule => Fails(rule)) || nested.Any(group => Fails(group)),
			FilterOperator.Or => rules.All(rule => Fails(rule)) && nested.All(group => Fails(group)),
		};
	}
}
