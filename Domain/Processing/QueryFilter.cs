namespace Domain.Processing;

public record QueryFilter
{
	public required IReadOnlyList<FilterGroup> Groups { get; init; }
	public required FilterOperator Operator { get; init; }
}
