using System.Diagnostics;
using System.Net.Http.Metrics;
using API.Http.Lanes;
using Contracts.Usage;
using Domain.Providers;
using Domain.Usage;
using Observability.Usage;
using Polly;
using Polly.Telemetry;
using Polly.Timeout;

namespace API.Http;

internal sealed record UsageStamp(RemoteService Service, UsageMark Mark)
{
	internal static readonly HttpRequestOptionsKey<UsageStamp> Key = new("sammlarr.usage");

	internal static readonly ResiliencePropertyKey<int> RateLimited = new("sammlarr.rate-limited");

	internal static readonly ResiliencePropertyKey<int> Retried = new("sammlarr.retried");

	internal static readonly ResiliencePropertyKey<bool> Sent = new("sammlarr.sent");

	internal IReadOnlyList<KeyValuePair<string, object?>> Tags()
	{
		List<KeyValuePair<string, object?>> tags = [new(UsageTags.Service, Service.ToString()), new(UsageTags.Cause, Mark.Cause.ToString())];
		Mark.Feed.Apply(feed => tags.Add(new(UsageTags.Feed, feed.ToString())));
		return tags;
	}

	internal static Optional<UsageStamp> Of(HttpRequestMessage? request) => Optional.Of(request).Map(found => Stamped(found));

	private static Optional<UsageStamp> Stamped(HttpRequestMessage request)
	{
		request.Options.TryGetValue(Key, out var stamp);
		return Optional.Of(stamp);
	}
}

internal sealed class UsageStampHandler(RemoteService service) : DelegatingHandler
{
	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		request.Options.Set(UsageStamp.Key, new UsageStamp(service, UsageCauses.Current));
		return base.SendAsync(request, cancellationToken);
	}
}

internal sealed class UsageTagsHandler(IFeedRunUsage feedRuns) : DelegatingHandler
{
	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		var stamp = UsageStamp.Of(request);
		stamp.Apply(found => HttpMetricsEnrichmentContext.AddCallback(request, context => Tag(context, found)));
		var started = Stopwatch.GetTimestamp();
		try
		{
			return await base.SendAsync(request, cancellationToken);
		}
		finally
		{
			var took = Stopwatch.GetElapsedTime(started);
			stamp.Apply(found => found.Mark.Feed.Apply(feed => feedRuns.Time(feed, found.Service, DurationBuckets.Of(took.TotalMilliseconds))));
		}
	}

	private static void Tag(HttpMetricsEnrichmentContext context, UsageStamp stamp)
	{
		foreach (var tag in stamp.Tags())
		{
			context.AddCustomTag(tag.Key, tag.Value);
		}
	}
}

internal sealed class UsageMeteringEnricher(IFeedRunUsage feedRuns) : MeteringEnricher
{
	private const string PipelineExecuted = "PipelineExecuted";

	public override void Enrich<TResult, TArgs>(in EnrichmentContext<TResult, TArgs> context)
	{
		var outcome = context.TelemetryEvent.Outcome;
		var exception = outcome?.Exception;
		var resilience = context.TelemetryEvent.Context;
		var neverSent = !resilience.Properties.GetValue(UsageStamp.Sent, false);
		if (context.TelemetryEvent.Event.EventName != PipelineExecuted || !IsRetryPipeline(context.TelemetryEvent.Source.PipelineName) || neverSent)
		{
			return;
		}

		var status = Status(outcome);
		var classified = UsageOutcomes.Classify(status, Fault(exception), resilience.Properties.GetValue(UsageStamp.RateLimited, 0));
		var stamp = UsageStamp.Of(resilience.GetRequestMessage());
		stamp.Apply(found => Credit(found, classified));
		var stamped = stamp.Map(found => found.Tags().Append(new(UsageTags.Outcome, classified.ToString()))).GetValueOr([]);
		foreach (var tag in stamped)
		{
			context.Tags.Add(tag);
		}
	}

	private static bool IsRetryPipeline(string? pipeline) => pipeline?.EndsWith($"-{LanedPipeline.RetryPipeline}", StringComparison.Ordinal) ?? false;

	private void Credit(UsageStamp stamp, UsageOutcome outcome) => stamp.Mark.Feed.Apply(feed => feedRuns.Credit(feed, stamp.Service, outcome, 1));

	private static UsageFault Fault(Exception? exception)
	{
		return exception switch
		{
			null => UsageFault.None,
			TimeoutRejectedException => UsageFault.TimedOut,
			OperationCanceledException => UsageFault.Cancelled,
			_ => UsageFault.Other,
		};
	}

	private static Optional<int> Status<TResult>(Outcome<TResult>? outcome)
	{
		return Optional.Of(outcome).Map(ended => Optional.Of(ended.Result as HttpResponseMessage)).Map(response => (int)response.StatusCode);
	}
}
