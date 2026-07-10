using Domain.Feed;
using Domain.Processing;
using Domain.Providers;

namespace Contracts.Feed;

public record FeedCreateData
{
	public required bool IsEnabled { get; init; }
	public required FeedProvider Provider { get; init; }
	public required FeedAddStrategy Strategy { get; init; }
	public required string Identifier { get; init; }
	public QueryFilter? Filter { get; init; }
	public QuerySort? Sort { get; init; }
	public QueryLimit? Limit { get; init; }
	public required SyncMode Schedule { get; init; }
}
