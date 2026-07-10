using Domain.Processing;

namespace Contracts.Processing;

public record QueryOptions
{
	public QueryFilter? Filter { get; init; }
	public QuerySort? Sort { get; init; }
	public QueryLimit? Limit { get; init; }
}
