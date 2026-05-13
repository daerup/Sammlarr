using System.Diagnostics;
using System.Diagnostics.Metrics;
using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Feed;
using Contracts.Media.Metadata;
using Contracts.Media.Search;

namespace Shared.Telemetry;

public static class SammlarrTelemetry
{
	public const string ServiceName = "Sammlarr";
	public const string MeterName = "Sammlarr";

	private static readonly ActivitySource Source = new(ServiceName);
	private static readonly Meter Meter = new(MeterName);

	private static readonly Counter<long> CacheHits = Meter.CreateCounter<long>("sammlarr.metadata.cache.hits");
	private static readonly Counter<long> CacheMisses = Meter.CreateCounter<long>("sammlarr.metadata.cache.misses");
	private static readonly Counter<long> CacheStale = Meter.CreateCounter<long>("sammlarr.metadata.cache.stale");
	private static readonly Counter<long> CachePartial = Meter.CreateCounter<long>("sammlarr.metadata.cache.partial");
	private static readonly Counter<long> IdResolutionOutcomes = Meter.CreateCounter<long>("sammlarr.id_resolution.outcomes");
	private static readonly Counter<long> FeedLoads = Meter.CreateCounter<long>("sammlarr.feed.load");
	private static readonly Counter<long> WatchlistLoads = Meter.CreateCounter<long>("sammlarr.watchlist.load");
	private static readonly Counter<long> BlacklistLoads = Meter.CreateCounter<long>("sammlarr.blacklist.load");
	private static readonly Counter<long> TrendingLoads = Meter.CreateCounter<long>("sammlarr.trending.load");

	private static readonly HashSet<string> ExcludedBlazorHubMethods = ["OnRenderCompleted", "EndInvokeJSFromDotNet"];

	public static Activity? StartMetadataItem(IMediaId mediaId)
	{
		var activity = Source.StartActivity("metadata.item");
		activity?.DisplayName = $"FETCH {mediaId}";
		return activity;
	}

	public static void TagItemOutcome(Activity? activity, MetadataItemOutcome outcome, MediaType mediaType)
	{
		var counter = outcome switch
		{
			MetadataItemOutcome.Hit => CacheHits,
			MetadataItemOutcome.Miss => CacheMisses,
			MetadataItemOutcome.Stale => CacheStale,
			MetadataItemOutcome.Partial => CachePartial,
		};
		counter.Add(1, new KeyValuePair<string, object?>("media.type", mediaType.ToString()));

		if (activity is null)
		{
			return;
		}
		activity.SetTag("metadata.outcome", outcome.ToString().ToLowerInvariant());
		activity.DisplayName += $" [{outcome.ToString().ToUpperInvariant()}]";
	}

	public static void TagIdResolutionOutcome(Activity? activity, IdResolutionOutcome outcome, MediaProvider target)
	{
		IdResolutionOutcomes.Add(
			1,
			new KeyValuePair<string, object?>("target", target.ToString()),
			new KeyValuePair<string, object?>("outcome", outcome.ToString().ToLowerInvariant())
		);

		if (activity is null)
		{
			return;
		}
		activity.SetTag("id_resolution.outcome", outcome.ToString().ToLowerInvariant());
		activity.DisplayName += $" [{outcome.ToString().ToUpperInvariant()}]";
	}

	public static Activity? StartMetadataLookup(MediaSearchProvider provider, IMediaId id)
	{
		var activity = Source.StartActivity("metadata.lookup");
		activity?.DisplayName = $"LOOKUP {provider} → {id}";
		return activity;
	}

	public static Activity? StartMetadataEnrich(IMediaId fromId, IEnumerable<string> missingFields)
	{
		const string metadata = "metadata";
		var fields = missingFields.ToArray();
		var activity = Source.StartActivity($"{metadata}.enrich");
		activity?.SetTag($"{metadata}.missing_fields", fields);
		activity?.DisplayName = $"ENRICH ← {fromId} ({string.Join(", ", fields)})";
		return activity;
	}

	public static Activity? StartMetadataRatings(RatingProvider provider)
	{
		var activity = Source.StartActivity("metadata.ratings");
		activity?.DisplayName = $"APPLY RATINGS {provider}";
		return activity;
	}

	public static Activity? StartIdResolution(MediaProvider target, IEnumerable<IMediaId> candidates, string? title = null)
	{
		const string idResolution = "id_resolution";
		var activity = Source.StartActivity($"media.{idResolution}");
		if (activity is null)
		{
			return null;
		}
		var candidateLabels = candidates.Select(c => c.ToString()).ToArray();
		activity.SetTag($"{idResolution}.candidates", candidateLabels);
		activity.SetTag($"{idResolution}.title", title);
		activity.DisplayName = $"RESOLVE → {target}";
		return activity;
	}

	public static Activity? StartBlacklistLoad(WatchlistProvider provider)
	{
		BlacklistLoads.Add(1, new KeyValuePair<string, object?>("provider", provider.ToString()));
		var activity = Source.StartActivity("blacklist.load");
		activity?.DisplayName = $"BLACKLIST {provider}";
		return activity;
	}

	public static Activity? StartWatchlistLoad(WatchlistProvider provider)
	{
		var activity = Source.StartActivity("watchlist.load");
		activity?.DisplayName = $"WATCHLIST FULL {provider}";
		return activity;
	}

	public static Activity? StartWatchlistLoad(WatchlistProvider provider, int page)
	{
		WatchlistLoads.Add(1, new KeyValuePair<string, object?>("provider", provider.ToString()));
		var activity = Source.StartActivity("watchlist.load");
		activity?.DisplayName = $"WATCHLIST PAGED {provider}";
		activity?.SetTag("watchlist.page", page);
		return activity;
	}

	public static Activity? StartFeedLoad(FeedProvider provider, string identifier)
	{
		FeedLoads.Add(1, new KeyValuePair<string, object?>("provider", provider.ToString()));
		var activity = Source.StartActivity("feed.load");
		activity?.DisplayName = $"FEED {provider}:{identifier}";
		return activity;
	}

	public static Activity? StartFeedItem(string? title, int year, IEnumerable<IMediaId> ids)
	{
		var activity = Source.StartActivity("feed.item");
		if (activity is null)
		{
			return null;
		}
		activity.SetTag("feed.item.title", title);
		activity.SetTag("feed.item.year", year);
		activity.SetTag("feed.item.ids", ids.Select(id => id.ToString()).ToArray());
		activity.DisplayName = year > 0 ? $"ITEM {title} ({year})" : $"ITEM {title}";
		return activity;
	}

	public static Activity? StartTrendingLoad(int page)
	{
		TrendingLoads.Add(1);
		var activity = Source.StartActivity("trending.load");
		activity?.DisplayName = $"TRENDING [{page}]";
		activity?.SetTag("trending.page", page);
		return activity;
	}

	public static Activity? StartBlazorHubInvocation(string hubMethodName, string? circuitId)
	{
		if (ExcludedBlazorHubMethods.Contains(hubMethodName))
		{
			return null;
		}
		var activity = Source.StartActivity("blazor." + hubMethodName, ActivityKind.Server);
		activity?.SetTag("circuit.id", circuitId);
		return activity;
	}

	public static void TagItemSuccess(Activity? activity, bool success) => activity?.SetTag("success", success);

	public static void TagCircuitId(Activity? activity, string? circuitId) => activity?.SetTag("circuit.id", circuitId);
}

public enum MetadataItemOutcome
{
	Hit,
	Miss,
	Stale,
	Partial,
}

public enum IdResolutionOutcome
{
	Hit,
	Resolved,
	Unresolved,
}
