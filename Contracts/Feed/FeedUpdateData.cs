using Domain.Feed;
using Domain.Processing;
using Domain.Providers;

namespace Contracts.Feed;

public record FeedUpdateData
{
	public required string Name { get; init; }
	public required bool IsEnabled { get; init; }
	public required FeedProvider Provider { get; init; }
	public required FeedStrategy Strategy { get; init; }
	public required string Identifier { get; init; }
	public Optional<QueryFilter> Filter { get; init; } = new None();
	public Optional<QuerySort> Sort { get; init; } = new None();
	public Optional<QueryLimit> Limit { get; init; } = new None();
	public required FeedRunTrigger Schedule { get; init; }
}
