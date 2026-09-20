using Domain.Feed;
using Domain.Feed.Run;

namespace Contracts.Feed;

public sealed record FeedRunRecord
{
	public required FeedId FeedId { get; init; }
	public required DateTimeOffset StartedAt { get; init; }
	public required TimeSpan Duration { get; init; }
	public required FeedStrategy Strategy { get; init; }
	public required FeedRunTrigger Trigger { get; init; }
	public required Conclusiveness Conclusiveness { get; init; }
	public required FeedRunCounts Counts { get; init; }
	public required IReadOnlyList<FeedRunItem> Items { get; init; }
}
