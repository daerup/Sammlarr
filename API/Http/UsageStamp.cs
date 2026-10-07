using System.Net.Http.Metrics;
using Domain.Providers;
using Domain.Usage;
using Observability.Usage;
using Polly;
using Polly.CircuitBreaker;
using Polly.Telemetry;
using Polly.Timeout;

namespace API.Http;

internal sealed record UsageStamp(RemoteService Service, UsageMark Mark)
{
	internal static readonly HttpRequestOptionsKey<UsageStamp> Key = new("sammlarr.usage");

	internal static readonly ResiliencePropertyKey<int> RateLimited = new("sammlarr.rate-limited");

	internal static readonly ResiliencePropertyKey<int> Retried = new("sammlarr.retried");

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

internal sealed class UsageTagsHandler : DelegatingHandler
{
	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		UsageStamp.Of(request).Apply(stamp => HttpMetricsEnrichmentContext.AddCallback(request, context => Tag(context, stamp)));
		return base.SendAsync(request, cancellationToken);
	}

	private static void Tag(HttpMetricsEnrichmentContext context, UsageStamp stamp)
	{
		foreach (var tag in stamp.Tags())
		{
			context.AddCustomTag(tag.Key, tag.Value);
		}
	}
}

internal sealed class UsageMeteringEnricher : MeteringEnricher
{
	private const string PipelineExecuted = "PipelineExecuted";

	public override void Enrich<TResult, TArgs>(in EnrichmentContext<TResult, TArgs> context)
	{
		var outcome = context.TelemetryEvent.Outcome;
		var exception = outcome?.Exception;
		var resilience = context.TelemetryEvent.Context;
		var neverSent = exception is BrokenCircuitException && resilience.Properties.GetValue(UsageStamp.Retried, 0) == 0;
		if (context.TelemetryEvent.Event.EventName != PipelineExecuted || neverSent)
		{
			return;
		}

		var status = Status(outcome);
		var classified = UsageOutcomes.Classify(status, Fault(exception), resilience.Properties.GetValue(UsageStamp.RateLimited, 0));
		var stamped = UsageStamp.Of(resilience.GetRequestMessage()).Map(stamp => stamp.Tags().Append(new(UsageTags.Outcome, classified.ToString()))).GetValueOr([]);
		foreach (var tag in stamped)
		{
			context.Tags.Add(tag);
		}
	}

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
