using DB.Data.Feed;
using DB.Data.FeedRun;
using DB.Data.Media;
using Domain.Providers;
using Domain.Watchlist;

namespace DB.Data.Watchlist;

internal record WatchlistRow : IAuditable, IUserOwned
{
	public required MediaRow Media { get; set; }

	public required WatchlistEntryOrigin Origin { get; set; }

	public WatchlistProvider? RemoteProvider { get; set; }

	public ManualSource? ManualSource { get; set; }

	public Guid? SourceFeedId { get; set; }
	public FeedRow? SourceFeed { get; set; }
	public string? SourceFeedNameSnapshot { get; set; }
	public Guid? SourceRunId { get; set; }
	public FeedRunRow? SourceRun { get; set; }

	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset UpdatedAt { get; set; }
}
