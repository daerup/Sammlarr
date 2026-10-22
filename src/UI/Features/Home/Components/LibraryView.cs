using Domain.Media;
using Domain.Providers;
using Domain.Wantlist;
using Domain.Wantlist.Arr;
using UI.Features.Wantlist.Components;
using UI.Shared.Services.Branding;

namespace UI.Features.Home.Components;

public sealed record SourceShare(string Name, int Count, Optional<string> Color);

internal static class LibraryView
{
	internal const string HandColor = "var(--bar-hand)";

	internal static IReadOnlyList<SourceShare> Sources(
		WantlistSnapshot wantlist,
		IReadOnlyDictionary<MediaId, MediaType> types,
		MediaProviderBrandingService branding
	)
	{
		return
		[
			.. wantlist
				.Items.Select(item => Of(item, types, branding))
				.GroupBy(origin => (origin.Tier, origin.Name))
				.OrderBy(group => group.Key.Tier)
				.ThenByDescending(group => group.Count())
				.ThenBy(group => group.Key.Name, StringComparer.Ordinal)
				.Select(group => new SourceShare(group.Key.Name, group.Count(), group.First().Color)),
		];
	}

	internal static IReadOnlyList<MediaId> ArrTitles(WantlistSnapshot wantlist)
	{
		return [.. wantlist.Items.Where(item => item.Provenance is SinkOrigin { Provider: SinkKind.Arr }).Select(item => item.MediaId)];
	}

	private static Origin Of(WantlistItem item, IReadOnlyDictionary<MediaId, MediaType> types, MediaProviderBrandingService branding)
	{
		var type = types.GetValueOrNone(item.MediaId).GetValueOr(MediaType.Unknown);
		return item.Provenance switch
		{
			FeedOrigin feed => new Origin(Tier.Feed, feed.Feed.Name, new None()),
			OrphanedOrigin orphaned => new Origin(Tier.DeletedFeed, $"{orphaned.FormerFeedName} (deleted)", new None()),
			SinkOrigin sink => new Origin(
				TierOf(sink.Provider, type),
				$"From {ProvenanceView.Label(item.Provenance, branding, type)}",
				Optional.Of(ProvenanceView.BrandColor(item.Provenance, branding, type))
			),
			ManualOrigin => new Origin(Tier.Hand, "Added by hand", Some.Of(HandColor)),
		};
	}

	private static Tier TierOf(SinkKind sink, MediaType type)
	{
		return sink switch
		{
			SinkKind.Plex => Tier.Plex,
			SinkKind.Arr => ArrApps.For(type).Map(ArrTier).GetValueOr(Tier.AnyArr),
		};
	}

	private static Tier ArrTier(ArrApp app)
	{
		return app switch
		{
			ArrApp.Radarr => Tier.Radarr,
			ArrApp.Sonarr => Tier.Sonarr,
		};
	}

	private sealed record Origin(Tier Tier, string Name, Optional<string> Color);

	private enum Tier
	{
		Feed,
		DeletedFeed,
		Plex,
		Radarr,
		Sonarr,
		AnyArr,
		Hand,
	}
}
