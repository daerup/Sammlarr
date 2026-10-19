using System.Globalization;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist;
using Domain.Wantlist.Arr;
using UI.Shared.Services.Branding;
using UrlGuid = Shared.UrlGuid;

namespace UI.Features.Wantlist.Components;

internal static class ProvenanceView
{
	internal static string Icon(WantlistItemProvenance provenance) =>
		provenance.Value switch
		{
			SinkOrigin => "cloud",
			ManualOrigin manual => ManualIcon(manual.Source),
			FeedOrigin => "rss",
			OrphanedOrigin => "ghost",
			_ => "circle",
		};

	internal static string Label(WantlistItemProvenance provenance, MediaProviderBrandingService branding, MediaType type)
	{
		return provenance switch
		{
			SinkOrigin sink => SinkName(sink.Provider, branding, type),
			ManualOrigin => "Manual",
			FeedOrigin feed => feed.Feed.Name,
			OrphanedOrigin orphaned => orphaned.FormerFeedName,
		};
	}

	internal static string Heading(WantlistItemProvenance provenance) =>
		provenance.Value switch
		{
			SinkOrigin => "Media service",
			ManualOrigin => "Manual",
			FeedOrigin feed => feed.Feed.Name,
			OrphanedOrigin orphaned => orphaned.FormerFeedName,
			_ => string.Empty,
		};

	internal static IReadOnlyList<BrandingInfo> Brands(WantlistItemProvenance provenance, MediaProviderBrandingService branding, MediaType type)
	{
		return provenance switch
		{
			SinkOrigin sink => App(sink.Provider, type)
				.Map(app => (IReadOnlyList<BrandingInfo>)[branding.GetBrandingInfo(app.Remote)])
				.GetValueOr(() => branding.Brands(sink.Provider.Service)),
			FeedOrigin feed => branding.Brands(feed.Feed.Provider.Service),
			ManualOrigin or OrphanedOrigin => [],
		};
	}

	internal static string? EditUrl(WantlistItemProvenance provenance) =>
		provenance.Value is FeedOrigin feed ? $"/feeds/edit/{UrlGuid.Encode(feed.Feed.Id.Value)}" : null;

	internal static FeedRunCounts? RunCounts(WantlistItemProvenance provenance) => provenance is FeedOrigin feed ? feed.Run.Counts : null;

	internal static string? BrandColor(WantlistItemProvenance provenance, MediaProviderBrandingService branding, MediaType type)
	{
		return provenance switch
		{
			SinkOrigin sink => App(sink.Provider, type)
				.Map(app => branding.GetBrandingInfo(app.Remote).BrandColor)
				.GetValueOr(() => branding.Color(sink.Provider.Service)),
			FeedOrigin feed => branding.Color(feed.Feed.Provider.Service),
			ManualOrigin or OrphanedOrigin => null,
		};
	}

	internal static string Description(WantlistItemProvenance provenance, MediaProviderBrandingService branding, DateTimeOffset addedAt, MediaType type)
	{
		var when = addedAt.LocalDateTime.ToString("MMM d, yyyy HH:mm", CultureInfo.InvariantCulture);

		return provenance.Value switch
		{
			SinkOrigin sink => $"Added on {SinkName(sink.Provider, branding, type)}",
			ManualOrigin manual => manual.Source == ManualSource.Unknown ? $"Added manually on {when}" : $"Added from {ManualName(manual.Source)} on {when}",
			FeedOrigin feed => $"Added by feed '{feed.Feed.Name}' on {when}",
			OrphanedOrigin orphaned => $"Was from '{orphaned.FormerFeedName}' (deleted)",
			_ => string.Empty,
		};
	}

	internal static string? RunUrl(WantlistItemProvenance provenance, MediaId? itemId) =>
		provenance.Value is FeedOrigin feed ? $"/feeds/{UrlGuid.Encode(feed.Feed.Id.Value)}/runs/{UrlGuid.Encode(feed.Run.Id.Value)}{ItemQuery(itemId)}" : null;

	private static string ItemQuery(MediaId? itemId) => itemId is { } id ? $"?item={UrlGuid.Encode(id.Value)}" : string.Empty;

	private static string SinkName(SinkKind sink, MediaProviderBrandingService branding, MediaType type) =>
		App(sink, type).Map(app => app.Name).GetValueOr(() => branding.Name(sink.Service));

	private static Optional<ArrApp> App(SinkKind sink, MediaType type) => ArrApps.For(type).When(_ => sink == SinkKind.Arr);

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
