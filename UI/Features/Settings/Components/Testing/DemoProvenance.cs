using Domain.Feed;
using Domain.Providers;
using Domain.Watchlist;

namespace UI.Features.Settings.Components.Testing;

internal static class DemoProvenance
{
	public static readonly DateTimeOffset AddedAt = DemoFeeds.Now.AddDays(-4);

	public static readonly WatchlistItemProvenance Feed = new FeedOrigin(DemoFeeds.MirrorFeed, DemoFeeds.MirrorRun);

	public static readonly WatchlistItemProvenance Remote = new RemoteOrigin(WatchlistProvider.Plex);

	public static readonly IReadOnlyList<WatchlistItemProvenance> All =
	[
		new ManualOrigin(ManualSource.Unknown),
		new ManualOrigin(ManualSource.Search),
		new ManualOrigin(ManualSource.Browse),
		new ManualOrigin(ManualSource.FeedPreview),
		Remote,
		new FeedOrigin(DemoFeeds.TrendingFeed, DemoFeeds.FailedRun),
		Feed,
		new OrphanedOrigin("Old feed"),
	];
}
