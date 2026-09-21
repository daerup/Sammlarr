using Domain.Feed.Run;
using Domain.Media;
using Domain.Providers;
using Domain.Watchlist;
using UI.Shared.Services.Branding;
using UrlGuid = global::Shared.UrlGuid;

namespace UI.Features.Watchlist.Components;

internal static class ProvenanceView
{
	internal static string Icon(WatchlistItemProvenance provenance) =>
		provenance.Value switch
		{
			RemoteOrigin => "cloud",
			ManualOrigin manual => ManualIcon(manual.Source),
			FeedOrigin => "rss",
			OrphanedOrigin => "unlink",
			_ => "circle",
		};

	internal static string Label(WatchlistItemProvenance provenance, MediaProviderBrandingService branding) =>
		provenance.Value switch
		{
			RemoteOrigin remote => ProviderName(remote.Provider, branding),
			ManualOrigin => "Manual",
			FeedOrigin feed => feed.Feed.Name,
			OrphanedOrigin orphaned => orphaned.FormerFeedName,
			_ => string.Empty,
		};

	internal static string Heading(WatchlistItemProvenance provenance) =>
		provenance.Value switch
		{
			RemoteOrigin => "Remote watchlist",
			ManualOrigin => "Manual",
			FeedOrigin feed => feed.Feed.Name,
			OrphanedOrigin orphaned => orphaned.FormerFeedName,
			_ => string.Empty,
		};

	internal static MediaProvider? Provider(WatchlistItemProvenance provenance) =>
		provenance.Value switch
		{
			RemoteOrigin remote => remote.Provider.AsMediaProvider(),
			FeedOrigin feed => feed.Feed.Provider.AsMediaProvider(),
			_ => null,
		};

	internal static string? EditUrl(WatchlistItemProvenance provenance) =>
		provenance.Value is FeedOrigin feed ? $"/feeds/edit/{UrlGuid.Encode(feed.Feed.Id.Value)}" : null;

	internal static FeedRunCounts? RunCounts(WatchlistItemProvenance provenance) => provenance is FeedOrigin feed ? feed.Run.Counts : null;

	internal static string? BrandColor(WatchlistItemProvenance provenance, MediaProviderBrandingService branding) =>
		provenance.Value switch
		{
			RemoteOrigin remote => branding.GetBrandingInfo(remote.Provider.AsMediaProvider()).BrandColor,
			FeedOrigin feed => branding.GetBrandingInfo(feed.Feed.Provider.AsMediaProvider()).BrandColor,
			_ => null,
		};

	internal static string Description(WatchlistItemProvenance provenance, DateTimeOffset addedAt, MediaProviderBrandingService branding)
	{
		var when = addedAt.LocalDateTime.ToString("MMM d, yyyy HH:mm");

		return provenance.Value switch
		{
			RemoteOrigin remote => $"From your remote {ProviderName(remote.Provider, branding)} watchlist",
			ManualOrigin manual => manual.Source == ManualSource.Unknown ? $"Added manually on {when}" : $"Added from {ManualName(manual.Source)} on {when}",
			FeedOrigin feed => $"Added by feed '{feed.Feed.Name}' on {when}",
			OrphanedOrigin orphaned => $"Was from '{orphaned.FormerFeedName}' (deleted)",
			_ => string.Empty,
		};
	}

	internal static string? RunUrl(WatchlistItemProvenance provenance, MediaId? itemId) =>
		provenance.Value is FeedOrigin feed ? $"/feeds/{UrlGuid.Encode(feed.Feed.Id.Value)}/runs/{UrlGuid.Encode(feed.Run.Id)}{ItemQuery(itemId)}" : null;

	private static string ItemQuery(MediaId? itemId) => itemId is { } id ? $"?item={UrlGuid.Encode(id.Value)}" : string.Empty;

	private static string ProviderName(WatchlistProvider provider, MediaProviderBrandingService branding) =>
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
