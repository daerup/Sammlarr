using Contracts.Processing;

namespace Contracts.Feed;

public record Feed
{
	public required string Name { get; init; }
	public required Guid Id { get; init; }
	public required bool Enabled { get; init; }
	public required FeedProvider Provider { get; init; }
	public required FeedAddStrategy Strategy { get; init; }
	public required string Identifier { get; init; }
	public required DateTimeOffset CreatedAt { get; init; }
	public QueryFilter? Filter { get; init; }
	public QuerySort? Sort { get; init; }
	public QueryLimit? Limit { get; init; }
	public required SyncMode Schedule { get; init; }
}
