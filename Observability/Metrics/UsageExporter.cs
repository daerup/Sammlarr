using Contracts;
using Contracts.Usage;
using Domain.Feed;
using Domain.Providers;
using Domain.Usage;
using Microsoft.Extensions.DependencyInjection;
using Observability.Usage;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Observability.Metrics;

internal sealed class UsageExporter(IServiceScopeFactory scopes, IUsageLastCalls lastCalls, IDateTimeProvider clock) : BaseExporter<Metric>
{
	internal const string PipelineDuration = "resilience.polly.pipeline.duration";

	internal const string RequestDuration = "http.client.request.duration";

	public override ExportResult Export(in Batch<Metric> batch)
	{
		var calls = new List<UsageCalls>();
		var attempts = new List<UsageAttempts>();
		foreach (var metric in batch)
		{
			if (metric.Name is not (PipelineDuration or RequestDuration))
			{
				continue;
			}

			foreach (ref readonly var point in metric.GetMetricPoints())
			{
				Read(metric.Name, point, calls, attempts);
			}
		}

		var rows = UsageFold.Of(calls, attempts);
		foreach (var call in rows.Calls.Where(call => call.Outcome is not UsageOutcome.Cancelled))
		{
			lastCalls.Saw(call.Service, clock.Now);
		}

		Write(rows);
		return ExportResult.Success;
	}

	internal static void Read(string metric, in MetricPoint point, List<UsageCalls> calls, List<UsageAttempts> attempts)
	{
		var tags = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var tag in point.Tags)
		{
			tags[tag.Key] = $"{tag.Value}";
		}

		var service = Parsed<RemoteService>(tags, UsageTags.Service);
		var cause = Parsed<UsageCause>(tags, UsageTags.Cause).GetValueOr(UsageCause.Other);
		var feed = tags.GetValueOrNone(UsageTags.Feed).Map(value => Optional.When(Guid.TryParse(value, out var id), () => FeedId.From(id)));
		var hour = Hour(point.EndTime);
		var count = point.GetHistogramCount();
		switch (metric)
		{
			case PipelineDuration:
				service.Apply(found =>
					Parsed<UsageOutcome>(tags, UsageTags.Outcome).Apply(outcome => calls.Add(new UsageCalls(found, hour, cause, outcome, feed, count)))
				);
				break;
			case RequestDuration:
				var buckets = Buckets(point);
				service.Apply(found => attempts.Add(new UsageAttempts(found, hour, cause, feed, count, buckets)));
				break;
		}
	}

	internal static DateTimeOffset Hour(DateTimeOffset at)
	{
		var utc = at.ToUniversalTime();
		return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
	}

	private static Dictionary<int, long> Buckets(in MetricPoint point)
	{
		var buckets = new Dictionary<int, long>();
		foreach (var bucket in point.GetHistogramBuckets())
		{
			if (bucket.BucketCount > 0)
			{
				var key = DurationBuckets.Of(bucket.ExplicitBound * 1000);
				buckets[key] = buckets.GetValueOrDefault(key) + bucket.BucketCount;
			}
		}

		return buckets;
	}

	private static Optional<TEnum> Parsed<TEnum>(Dictionary<string, string> tags, string key)
		where TEnum : struct, Enum
	{
		return tags.GetValueOrNone(key).Map(value => Optional.When(Enum.TryParse<TEnum>(value, out var parsed), () => parsed));
	}

	private void Write(UsageRows rows)
	{
		if (rows.Calls.Count == 0 && rows.Attempts.Count == 0)
		{
			return;
		}

		using var scope = scopes.CreateScope();
		scope.ServiceProvider.GetRequiredService<IUsageStore>().AddAsync(rows).GetAwaiter().GetResult();
	}
}
