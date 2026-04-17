namespace Contracts.Processing;

public record QueryOptions
{
	public QueryFilter? Filter { get; init; }
	public QuerySort? Sort { get; init; }
	public int? Limit { get; init; }
}
