using Contracts.Media;

namespace Contracts.Feed;

public record FeedItem
{
	public required int Year { get; init; }
	public required string Title { get; init; }
	public required Feed Source { get; init; }
	public required IExternalMediaId WatchlistExternalId { get; init; }
	public required IEnumerable<IExternalMediaId> AdditionalExternals { get; init; }
}
