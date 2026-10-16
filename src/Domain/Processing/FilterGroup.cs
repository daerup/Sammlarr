namespace Domain.Processing;

public record FilterGroup
{
	public IReadOnlyList<FilterPredicate> Predicates { get; init; } = [];
	public IReadOnlyList<FilterGroup> Groups { get; init; } = [];
	public required FilterOperator Operator { get; init; }
}
