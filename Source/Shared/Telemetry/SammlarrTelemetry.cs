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
	private static readonly Counter<long> IdResolutionOutcomes = Meter.CreateCounter<long>("sammlarr.id_resolution.outcomes");
	private static readonly Counter<long> FeedLoads = Meter.CreateCounter<long>("sammlarr.feed.load");
	private static readonly Counter<long> WatchlistLoads = Meter.CreateCounter<long>("sammlarr.watchlist.load");
	private static readonly Counter<long> BlacklistLoads = Meter.CreateCounter<long>("sammlarr.blacklist.load");
	private static readonly Counter<long> TrendingLoads = Meter.CreateCounter<long>("sammlarr.trending.load");

	private static readonly HashSet<string> ExcludedBlazorHubMethods = ["OnRenderCompleted", "EndInvokeJSFromDotNet"];

	public static Activity? StartMetadataBatch(int mediaCount)
	{
		var activity = Source.StartActivity("metadata.batch");
		activity?.DisplayName = $"METADATA BATCH {mediaCount} items";
		return activity;
	}

	public static Activity? StartMetadataItem(IMediaId mediaId, string state, ActivityContext parent = default)
	{
		var activity = Source.StartActivity("metadata.item", ActivityKind.Internal, parent);
		activity?.DisplayName = $"FETCH {mediaId} [{state}]";
		return activity;
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

	public static Activity? StartMetadataUpsert(int mediaCount, ActivityContext parent = default)
	{
		var activity = Source.StartActivity("metadata.upsert", ActivityKind.Internal, parent);
		activity?.DisplayName = $"METADATA UPSERT {mediaCount} items";
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
		var candidatesPart = candidateLabels.Length > 0 ? $" ← {string.Join(", ", candidateLabels)}" : string.Empty;
		activity.DisplayName = $"RESOLVE → {target}{title}{candidatesPart}";
		return activity;
	}

	public static Activity? StartBlacklistLoad(WatchlistProvider provider)
	{
		BlacklistLoads.Add(1, new KeyValuePair<string, object?>("provider", provider.ToString()));
		var activity = Source.StartActivity("blacklist.load");
		activity?.DisplayName = $"BLACKLIST {provider}";
		return activity;
	}

	public static Activity? StartWatchlistLoad(WatchlistProvider provider, int page)
	{
		WatchlistLoads.Add(1, new KeyValuePair<string, object?>("provider", provider.ToString()));
		var activity = Source.StartActivity("watchlist.load");
		activity?.DisplayName = $"WATCHLIST {provider} p{page}";
		return activity;
	}

	public static Activity? StartFeedLoad(FeedProvider provider, string identifier)
	{
		FeedLoads.Add(1, new KeyValuePair<string, object?>("provider", provider.ToString()));
		var activity = Source.StartActivity("feed.load");
		activity?.DisplayName = $"FEED {provider}:{identifier}";
		return activity;
	}

	public static Activity? StartTrendingLoad(int page)
	{
		TrendingLoads.Add(1);
		var activity = Source.StartActivity("trending.load");
		activity?.DisplayName = $"TRENDING p{page}";
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

	public static void RecordIdResolutionOutcome(MediaProvider target, string outcome) =>
		IdResolutionOutcomes.Add(1, new KeyValuePair<string, object?>("target", target.ToString()), new KeyValuePair<string, object?>("outcome", outcome));

	public static void RecordBatchCacheBreakdown(Activity? activity, int hits, int misses, int stale)
	{
		CacheHits.Add(hits);
		CacheMisses.Add(misses);
		CacheStale.Add(stale);

		if (activity is null)
		{
			return;
		}
		activity.SetTag("cache.hits", hits);
		activity.SetTag("cache.misses", misses);
		activity.SetTag("cache.stale", stale);
		activity.DisplayName = $"METADATA BATCH {hits + misses + stale} items ({hits} cached, {misses} fresh, {stale} stale)";
	}

	public static void TagItemSuccess(Activity? activity, bool success) => activity?.SetTag("success", success);

	public static void TagCircuitId(Activity? activity, string? circuitId) => activity?.SetTag("circuit.id", circuitId);
}
