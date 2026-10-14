using Domain.Processing;

namespace Core.Processing;

internal sealed record QueryOptions
{
	public Optional<QueryFilter> Filter { get; init; } = new None();
	public Optional<QuerySort> Sort { get; init; } = new None();
	public Optional<QueryLimit> Limit { get; init; } = new None();
	public Guid ShuffleSeed { get; init; }
}
