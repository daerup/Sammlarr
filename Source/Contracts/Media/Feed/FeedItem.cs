namespace Contracts.Media.Feed;

public record FeedItem
{
	public required int Year { get; init; }
	public required string Title { get; init; }
	public required Feed Source { get; init; }
	public required IMediaId WatchlistId { get; init; }
	public required IEnumerable<IMediaId> AdditionalIds { get; init; }
}
