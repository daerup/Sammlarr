using Domain.Providers;
using Domain.Wantlist;

namespace UI.Features.Settings.Components.Testing;

internal static class DemoProvenance
{
	public static readonly DateTimeOffset AddedAt = DemoFeeds.Now.AddDays(-4);

	public static readonly WantlistItemProvenance Feed = new FeedOrigin(DemoFeeds.MirrorFeed, DemoFeeds.MirrorRun);

	public static readonly WantlistItemProvenance Sink = new SinkOrigin(SinkKind.Plex);

	public static readonly IReadOnlyList<WantlistItemProvenance> All =
	[
		new ManualOrigin(ManualSource.Unknown),
		new ManualOrigin(ManualSource.Search),
		new ManualOrigin(ManualSource.Browse),
		new ManualOrigin(ManualSource.FeedPreview),
		Sink,
		new FeedOrigin(DemoFeeds.TrendingFeed, DemoFeeds.FailedRun),
		Feed,
		new OrphanedOrigin("Old feed"),
	];
}
