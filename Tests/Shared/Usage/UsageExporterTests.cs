using System.Diagnostics.Metrics;
using Contracts;
using Contracts.Usage;
using Domain.Feed;
using Domain.Providers;
using Domain.Usage;
using Microsoft.Extensions.DependencyInjection;
using Observability.Metrics;
using Observability.Usage;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using TestInfrastructure;

namespace Shared.Tests.Usage;

public class UsageExporterTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 7, 10, 30, 0, TimeSpan.Zero);

	private static readonly double[] Boundaries = [0, 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10];

	[Test]
	public async Task Export_ACallAndItsAttempts_WritesThemAsHourlyRows()
	{
		var feed = FeedId.From(Guid.NewGuid());
		var (store, added, lastCalls) = Mocks();
		using var meter = new Meter($"usage-{Guid.NewGuid()}");
		var calls = meter.CreateHistogram<double>(UsageExporter.PipelineDuration, "ms");
		var attempts = meter.CreateHistogram<double>(
			UsageExporter.RequestDuration,
			"s",
			null,
			null,
			new InstrumentAdvice<double> { HistogramBucketBoundaries = Boundaries }
		);
		var active = meter.CreateUpDownCounter<long>("http.client.active_requests");
		var events = meter.CreateCounter<long>("resilience.polly.strategy.events");
		KeyValuePair<string, object?>[] tags = [new(UsageTags.Service, "Tmdb"), new(UsageTags.Cause, "FeedRun"), new(UsageTags.Feed, feed.ToString())];
		using (Provider(meter, new UsageExporter(Scopes(store.Object), lastCalls.Object, Clock())))
		{
			calls.Record(250, [.. tags, new(UsageTags.Outcome, "AnsweredAfter429")]);
			attempts.Record(0.08, tags);
			attempts.Record(0.09, tags);
			attempts.Record(12, tags);
			active.Add(1, tags);
			events.Add(1, tags);
		}

		var rows = added.Values.OfType<UsageRows>().Single();
		await Assert
			.That(rows.Calls.Select(row => (row.Service, row.Cause, row.Outcome, row.Calls)))
			.IsEquivalentTo([(RemoteService.Tmdb, UsageCause.FeedRun, UsageOutcome.AnsweredAfter429, 1L)]);
		await Assert.That(rows.Calls.Single().Feed).IsSome().And.IsEqualTo(feed);
		await Assert.That(rows.Attempts.Single().Attempts).IsEqualTo(3);
		await Assert.That(rows.Attempts.Single().Buckets).IsEquivalentTo(new Dictionary<int, long> { [100] = 2, [DurationBuckets.Over] = 1 });
	}

	[Test]
	public async Task Export_AMeasurementWithoutAService_IsLeftOut()
	{
		var (store, added, lastCalls) = Mocks();
		using var meter = new Meter($"usage-{Guid.NewGuid()}");
		var calls = meter.CreateHistogram<double>(UsageExporter.PipelineDuration, "ms");
		using (Provider(meter, new UsageExporter(Scopes(store.Object), lastCalls.Object, Clock())))
		{
			calls.Record(250, new KeyValuePair<string, object?>(UsageTags.Outcome, "Answered"));
		}

		await Assert.That(added.Values).IsEmpty();
	}

	private static MeterProvider Provider(Meter meter, UsageExporter exporter)
	{
		return Sdk.CreateMeterProviderBuilder()
			.AddMeter(meter.Name)
			.AddReader(new BaseExportingMetricReader(exporter) { TemporalityPreference = MetricReaderTemporalityPreference.Delta })
			.Build();
	}

	private static (Mock<IUsageStore> Store, Arg<UsageRows> Added, Mock<IUsageLastCalls> LastCalls) Mocks()
	{
		var store = IUsageStore.Mock();
		var added = Any<UsageRows>();
		store.AddAsync(added, Any()).Returns(() => Task.CompletedTask);
		return (store, added, IUsageLastCalls.Mock());
	}

	private static IServiceScopeFactory Scopes(IUsageStore store)
	{
		return new ServiceCollection().AddSingleton(store).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
	}

	private static IDateTimeProvider Clock()
	{
		var clock = IDateTimeProvider.Mock();
		clock.Now.Getter.Returns(Now);
		return clock.Object;
	}
}
