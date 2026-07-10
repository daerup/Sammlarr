using DB.Data.Feed;
using DB.Data.Media;

namespace DB.Data.Watchlist;

internal record WatchlistEntity : IAuditable
{
	public required MediaRow Media { get; set; }

	public required WatchlistEntryOrigin Origin { get; set; }

	public Guid? SourceFeedId { get; set; }
	public FeedRow? SourceFeed { get; set; }
	public string? SourceFeedNameSnapshot { get; set; }

	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset UpdatedAt { get; set; }
}
