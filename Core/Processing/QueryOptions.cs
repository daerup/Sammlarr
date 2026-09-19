using Domain.Processing;

namespace Core.Processing;

internal record QueryOptions
{
	public QueryFilter? Filter { get; init; }
	public QuerySort? Sort { get; init; }
	public QueryLimit? Limit { get; init; }
}
