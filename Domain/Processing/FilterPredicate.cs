namespace Domain.Processing;

public record FilterPredicate
{
	public required string Field { get; init; }
	public required FilterPredicateOperator Operator { get; init; }
	public FilterValueKind ValueKind { get; init; } = FilterValueKind.String;
	public string? Value { get; init; }
	public string? ValueEnd { get; init; }
}
