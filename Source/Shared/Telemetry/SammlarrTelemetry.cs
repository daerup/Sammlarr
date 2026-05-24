using System.Diagnostics;
using System.Diagnostics.Metrics;
using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Feed;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Watchlist;
using Contracts.Watchlist.Sync;

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
	private static readonly Counter<long> WatchlistSyncs = Meter.CreateCounter<long>("sammlarr.watchlist.sync");
	private static readonly Counter<long> WatchlistSyncFailures = Meter.CreateCounter<long>("sammlarr.watchlist.sync.failures");
	private static readonly Counter<long> WatchlistPushFailures = Meter.CreateCounter<long>("sammlarr.watchlist.push.failures");
	private static readonly Counter<long> BlacklistLoads = Meter.CreateCounter<long>("sammlarr.blacklist.load");
	private static readonly Counter<long> TrendingLoads = Meter.CreateCounter<long>("sammlarr.trending.load");

	private static readonly HashSet<string> ExcludedBlazorHubMethods = ["OnRenderCompleted", "EndInvokeJSFromDotNet"];

	public static Activity? StartMetadataItem(MediaId mediaId)
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

	public static Activity? StartMetadataLookup(MediaSearchProvider provider, IExternalMediaId id)
	{
		var activity = Source.StartActivity("metadata.lookup");
		activity?.DisplayName = $"LOOKUP {provider} → {id}";
		return activity;
	}

	public static Activity? StartMetadataEnrich(IExternalMediaId fromId, IEnumerable<string> missingFields)
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

	public static Activity? StartIdResolution(MediaProvider target, IEnumerable<IExternalMediaId> candidates, string? title = null)
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

	public static Activity? StartWatchlistSync(WatchlistProvider provider, WatchlistSyncTrigger trigger)
	{
		var activity = Source.StartActivity("watchlist.sync");
		activity?.DisplayName = $"WATCHLIST SYNC {provider} ({trigger.Value})";
		activity?.SetTag("watchlist.provider", provider.ToString());
		activity?.SetTag("watchlist.trigger", trigger.Value.ToString());
		return activity;
	}

	public static Activity? StartWatchlistRemoteFetch(WatchlistProvider provider)
	{
		var activity = Source.StartActivity("watchlist.remote_fetch");
		activity?.DisplayName = $"WATCHLIST FETCH {provider}";
		activity?.SetTag("watchlist.provider", provider.ToString());
		return activity;
	}

	public static Activity? StartWatchlistPush(WatchlistProvider provider, string mutation)
	{
		var activity = Source.StartActivity("watchlist.push");
		activity?.DisplayName = $"WATCHLIST PUSH {provider} {mutation}";
		activity?.SetTag("watchlist.provider", provider.ToString());
		activity?.SetTag("watchlist.mutation", mutation);
		return activity;
	}

	public static void TagWatchlistSyncOutcome(Activity? activity, WatchlistSyncResult result, long durationMs)
	{
		var outcome = result.Value switch
		{
			WatchlistSyncSuccess => "success",
			WatchlistSyncUnchanged => "unchanged",
			WatchlistSyncAlreadyRunning => "already_running",
			WatchlistSyncRemoteFailed => "remote_failed",
			_ => "unknown",
		};

		var success = result.Value is WatchlistSyncSuccess s ? s : default;

		WatchlistSyncs.Add(
			1,
			new KeyValuePair<string, object?>("outcome", outcome),
			new KeyValuePair<string, object?>("inserted", success.Inserted),
			new KeyValuePair<string, object?>("confirmed", success.Confirmed),
			new KeyValuePair<string, object?>("hard_deleted", success.HardDeleted),
			new KeyValuePair<string, object?>("pushed_adds", success.PushedAdds),
			new KeyValuePair<string, object?>("pushed_removes", success.PushedRemoves),
			new KeyValuePair<string, object?>("remote_failures", success.RemoteFailures)
		);
		if (result.Value is not WatchlistSyncSuccess and not WatchlistSyncUnchanged)
		{
			WatchlistSyncFailures.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
		}
		if (activity is null)
		{
			return;
		}
		activity.SetTag("watchlist.outcome", outcome);
		activity.SetTag("watchlist.inserted", success.Inserted);
		activity.SetTag("watchlist.confirmed", success.Confirmed);
		activity.SetTag("watchlist.hard_deleted", success.HardDeleted);
		activity.SetTag("watchlist.pushed_adds", success.PushedAdds);
		activity.SetTag("watchlist.pushed_removes", success.PushedRemoves);
		activity.SetTag("watchlist.remote_failures", success.RemoteFailures);
		activity.SetTag("watchlist.duration_ms", durationMs);
	}

	public static void TagWatchlistPushOutcome(Activity? activity, bool success)
	{
		if (!success)
		{
			WatchlistPushFailures.Add(1);
		}
		activity?.SetTag("watchlist.push.success", success);
	}

	public static Activity? StartFeedLoad(FeedProvider provider, string identifier)
	{
		FeedLoads.Add(1, new KeyValuePair<string, object?>("provider", provider.ToString()));
		var activity = Source.StartActivity("feed.load");
		activity?.DisplayName = $"FEED {provider}:{identifier}";
		return activity;
	}

	public static Activity? StartFeedItem(string? title, int year, IEnumerable<IExternalMediaId> ids)
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
