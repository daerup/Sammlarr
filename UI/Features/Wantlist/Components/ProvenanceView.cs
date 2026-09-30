using Domain.Feed.Run;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist;
using UI.Shared.Services.Branding;
using UrlGuid = global::Shared.UrlGuid;

namespace UI.Features.Wantlist.Components;

internal static class ProvenanceView
{
	internal static string Icon(WantlistItemProvenance provenance) =>
		provenance.Value switch
		{
			SinkOrigin => "cloud",
			ManualOrigin manual => ManualIcon(manual.Source),
			FeedOrigin => "rss",
			OrphanedOrigin => "unlink",
			_ => "circle",
		};

	internal static string Label(WantlistItemProvenance provenance, MediaProviderBrandingService branding) =>
		provenance.Value switch
		{
			SinkOrigin sink => ProviderName(sink.Provider, branding),
			ManualOrigin => "Manual",
			FeedOrigin feed => feed.Feed.Name,
			OrphanedOrigin orphaned => orphaned.FormerFeedName,
			_ => string.Empty,
		};

	internal static string Heading(WantlistItemProvenance provenance) =>
		provenance.Value switch
		{
			SinkOrigin => "Media service",
			ManualOrigin => "Manual",
			FeedOrigin feed => feed.Feed.Name,
			OrphanedOrigin orphaned => orphaned.FormerFeedName,
			_ => string.Empty,
		};

	internal static MediaProvider? Provider(WantlistItemProvenance provenance) =>
		provenance.Value switch
		{
			SinkOrigin sink => sink.Provider.AsMediaProvider(),
			FeedOrigin feed => feed.Feed.Provider.AsMediaProvider(),
			_ => null,
		};

	internal static string? EditUrl(WantlistItemProvenance provenance) =>
		provenance.Value is FeedOrigin feed ? $"/feeds/edit/{UrlGuid.Encode(feed.Feed.Id.Value)}" : null;

	internal static FeedRunCounts? RunCounts(WantlistItemProvenance provenance) => provenance is FeedOrigin feed ? feed.Run.Counts : null;

	internal static string? BrandColor(WantlistItemProvenance provenance, MediaProviderBrandingService branding) =>
		provenance.Value switch
		{
			SinkOrigin sink => branding.GetBrandingInfo(sink.Provider.AsMediaProvider()).BrandColor,
			FeedOrigin feed => branding.GetBrandingInfo(feed.Feed.Provider.AsMediaProvider()).BrandColor,
			_ => null,
		};

	internal static string Description(WantlistItemProvenance provenance, DateTimeOffset addedAt, MediaProviderBrandingService branding)
	{
		var when = addedAt.LocalDateTime.ToString("MMM d, yyyy HH:mm");

		return provenance.Value switch
		{
			SinkOrigin sink => $"Added on {ProviderName(sink.Provider, branding)}",
			ManualOrigin manual => manual.Source == ManualSource.Unknown ? $"Added manually on {when}" : $"Added from {ManualName(manual.Source)} on {when}",
			FeedOrigin feed => $"Added by feed '{feed.Feed.Name}' on {when}",
			OrphanedOrigin orphaned => $"Was from '{orphaned.FormerFeedName}' (deleted)",
			_ => string.Empty,
		};
	}

	internal static string? RunUrl(WantlistItemProvenance provenance, MediaId? itemId) =>
		provenance.Value is FeedOrigin feed ? $"/feeds/{UrlGuid.Encode(feed.Feed.Id.Value)}/runs/{UrlGuid.Encode(feed.Run.Id)}{ItemQuery(itemId)}" : null;

	private static string ItemQuery(MediaId? itemId) => itemId is { } id ? $"?item={UrlGuid.Encode(id.Value)}" : string.Empty;

	private static string ProviderName(SinkKind provider, MediaProviderBrandingService branding) =>
		branding.GetBrandingInfo(provider.AsMediaProvider()).DisplayName;

	private static string ManualIcon(ManualSource source) =>
		source switch
		{
			ManualSource.Search => "search",
			ManualSource.Browse => "compass",
			ManualSource.FeedPreview => "flask-conical",
			ManualSource.Unknown => "user",
		};

	private static string ManualName(ManualSource source) =>
		source switch
		{
			ManualSource.Search => "Search",
			ManualSource.Browse => "Browse",
			ManualSource.FeedPreview => "feed preview",
			ManualSource.Unknown => "manual",
		};
}
