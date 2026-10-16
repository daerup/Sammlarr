using System.Diagnostics.Metrics;
using Contracts;
using Contracts.Usage;
using Domain.Providers;
using Domain.Usage;
using Harness;
using Microsoft.Extensions.DependencyInjection;
using Observability.Metrics;
using Observability.Usage;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using TestInfrastructure;

namespace Integration.Tests.Usage;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class UsageExportTests(SammlarrTestHost host) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task ReadAsync_AfterAnExportedCall_ReadsTheExportAsTheServicesLastCall()
	{
		var now = TimeProvider.System.GetUtcNow();
		host.SetCurrentTime(now);

		ExportATmdbCall();

		var report = await host.Resolve<IUsageReport>().ReadAsync(UsageRange.Day);
		await Assert.That(report.Current.Services.Single(line => line.Service == RemoteService.Tmdb).RightNow.LastCall).IsSome().And.IsEqualTo(now);
	}

	private void ExportATmdbCall()
	{
		using var meter = new Meter($"usage-{Guid.NewGuid()}");
		var calls = meter.CreateHistogram<double>(UsageExporter.PipelineDuration, "ms");
		var exporter = new UsageExporter(host.Resolve<IServiceScopeFactory>(), host.Resolve<IUsageLastCalls>(), host.Resolve<IDateTimeProvider>());
		using var meters = Sdk.CreateMeterProviderBuilder()
			.AddMeter(meter.Name)
			.AddReader(new BaseExportingMetricReader(exporter) { TemporalityPreference = MetricReaderTemporalityPreference.Delta })
			.Build();
		calls.Record(250, new KeyValuePair<string, object?>(UsageTags.Service, "Tmdb"), new KeyValuePair<string, object?>(UsageTags.Outcome, "Answered"));
	}
}
