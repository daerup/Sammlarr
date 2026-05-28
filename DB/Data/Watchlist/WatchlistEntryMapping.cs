using Contracts.Watchlist;

namespace DB.Data.Watchlist;

internal static class WatchlistEntryMapping
{
	public static WatchlistItemProvenance ToProvenance(this WatchlistEntity row) =>
		row.Origin switch
		{
			WatchlistEntryOrigin.Remote => new WatchlistItemProvenance(new RemoteOrigin()),
			WatchlistEntryOrigin.Manual => new WatchlistItemProvenance(new ManualOrigin()),
			WatchlistEntryOrigin.Feed => new WatchlistItemProvenance(
				new FeedOrigin(row.SourceFeedId ?? Guid.Empty, row.SourceFeed?.Name ?? row.SourceFeedNameSnapshot ?? string.Empty)
			),
			WatchlistEntryOrigin.Orphaned => new WatchlistItemProvenance(new OrphanedOrigin(row.SourceFeedNameSnapshot ?? string.Empty)),
		};

	public static void Apply(this WatchlistEntity row, WatchlistItemProvenance provenance)
	{
		switch (provenance.Value)
		{
			case RemoteOrigin:
				row.Origin = WatchlistEntryOrigin.Remote;
				row.SourceFeedId = null;
				row.SourceFeedNameSnapshot = null;
				break;
			case ManualOrigin:
				row.Origin = WatchlistEntryOrigin.Manual;
				row.SourceFeedId = null;
				row.SourceFeedNameSnapshot = null;
				break;
			case FeedOrigin feed:
				row.Origin = WatchlistEntryOrigin.Feed;
				row.SourceFeedId = feed.FeedId;
				row.SourceFeedNameSnapshot = null;
				break;
			case OrphanedOrigin orphaned:
				row.Origin = WatchlistEntryOrigin.Orphaned;
				row.SourceFeedId = null;
				row.SourceFeedNameSnapshot = orphaned.FormerFeedName;
				break;
		}
	}
}
