using DB.Data.Feed;
using DB.Data.Feed.Run;
using DB.Data.Media;
using Domain.Wantlist;

namespace DB.Data.Wantlist;

internal static class WantlistItemMapping
{
	public static WantlistItemProvenance ToProvenance(this WantlistRow row) =>
		row.Origin switch
		{
			WantlistItemOrigin.Sink => new WantlistItemProvenance(new SinkOrigin(row.Sink ?? default)),
			WantlistItemOrigin.Manual => new WantlistItemProvenance(new ManualOrigin(row.ManualSource ?? ManualSource.Unknown)),
			WantlistItemOrigin.Feed => row switch
			{
				{ SourceFeed: { } feed, SourceRun: { } run } => new WantlistItemProvenance(new FeedOrigin(feed.ToDomain(), run.ToDomain())),
				_ => new WantlistItemProvenance(new OrphanedOrigin(row.SourceFeed?.Name ?? row.SourceFeedNameSnapshot ?? string.Empty)),
			},
			WantlistItemOrigin.Orphaned => new WantlistItemProvenance(new OrphanedOrigin(row.SourceFeedNameSnapshot ?? string.Empty)),
		};

	public static WantlistRow ToRow(MediaRow media, WantlistItemProvenance provenance) =>
		provenance.Value switch
		{
			SinkOrigin sink => new WantlistRow
			{
				Media = media,
				Origin = WantlistItemOrigin.Sink,
				Sink = sink.Provider,
			},
			ManualOrigin manual => new WantlistRow
			{
				Media = media,
				Origin = WantlistItemOrigin.Manual,
				ManualSource = manual.Source,
			},
			FeedOrigin feed => new WantlistRow
			{
				Media = media,
				Origin = WantlistItemOrigin.Feed,
				SourceFeedId = feed.Feed.Id.Value,
				SourceRunId = feed.Run.Id,
			},
			OrphanedOrigin orphaned => new WantlistRow
			{
				Media = media,
				Origin = WantlistItemOrigin.Orphaned,
				SourceFeedNameSnapshot = orphaned.FormerFeedName,
			},
			_ => throw new InvalidOperationException($"Unhandled provenance '{provenance.Value?.GetType().Name}'."),
		};
}
