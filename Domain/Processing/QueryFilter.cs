namespace Domain.Processing;

public record QueryFilter
{
	public required List<FilterGroup> Groups { get; init; }
	public required FilterOperator Operator { get; init; }
}
