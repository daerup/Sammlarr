using System.ComponentModel.DataAnnotations;
using DB.Data.Feed;
using DB.Data.MediaGroup;

namespace DB.Data.Watchlist;

internal record WatchlistEntryEntity : Entity<Guid>
{
	[Required]
	public MediaIdentifierGroupEntity Media { get; set; } = null!;

	[Required]
	public WatchlistEntryOrigin Origin { get; set; }

	public Guid? SourceFeedId { get; set; }
	public FeedEntity? SourceFeed { get; set; }
	public string? SourceFeedNameSnapshot { get; set; }
}
