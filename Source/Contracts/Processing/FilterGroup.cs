namespace Contracts.Processing;

public record FilterGroup
{
	public List<string> Predicates { get; init; } = [];
	public List<FilterGroup> Groups { get; init; } = [];
	public required FilterOperator Operator { get; init; }
}
