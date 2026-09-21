using DB.Data.Feed;
using DB.Data.FeedRun;
using DB.Data.Media;
using Domain.Watchlist;

namespace DB.Data.Watchlist;

internal static class WatchlistEntryMapping
{
	public static WatchlistItemProvenance ToProvenance(this WatchlistRow row) =>
		row.Origin switch
		{
			WatchlistEntryOrigin.Remote => new WatchlistItemProvenance(new RemoteOrigin(row.RemoteProvider ?? default)),
			WatchlistEntryOrigin.Manual => new WatchlistItemProvenance(new ManualOrigin(row.ManualSource ?? ManualSource.Unknown)),
			WatchlistEntryOrigin.Feed => row switch
			{
				{ SourceFeed: { } feed, SourceRun: { } run } => new WatchlistItemProvenance(new FeedOrigin(feed.ToDomain(), run.ToDomain())),
				_ => new WatchlistItemProvenance(new OrphanedOrigin(row.SourceFeed?.Name ?? row.SourceFeedNameSnapshot ?? string.Empty)),
			},
			WatchlistEntryOrigin.Orphaned => new WatchlistItemProvenance(new OrphanedOrigin(row.SourceFeedNameSnapshot ?? string.Empty)),
		};

	public static WatchlistRow ToRow(MediaRow media, WatchlistItemProvenance provenance) =>
		provenance.Value switch
		{
			RemoteOrigin remote => new WatchlistRow
			{
				Media = media,
				Origin = WatchlistEntryOrigin.Remote,
				RemoteProvider = remote.Provider,
			},
			ManualOrigin manual => new WatchlistRow
			{
				Media = media,
				Origin = WatchlistEntryOrigin.Manual,
				ManualSource = manual.Source,
			},
			FeedOrigin feed => new WatchlistRow
			{
				Media = media,
				Origin = WatchlistEntryOrigin.Feed,
				SourceFeedId = feed.Feed.Id.Value,
				SourceRunId = feed.Run.Id,
			},
			OrphanedOrigin orphaned => new WatchlistRow
			{
				Media = media,
				Origin = WatchlistEntryOrigin.Orphaned,
				SourceFeedNameSnapshot = orphaned.FormerFeedName,
			},
			_ => throw new InvalidOperationException($"Unhandled provenance '{provenance.Value?.GetType().Name}'."),
		};
}
