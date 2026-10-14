using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using Shared.DI;

namespace Observability.Metrics;

internal static class MetricsPipeline
{
	private static readonly string[] Meters = ["System.Net.Http", "Polly"];

	public static void Register(IServiceCollection services, ObservabilityContext context)
	{
		services.AddSingleton<UsageExporter>();
		services.AddHostedService<UsageFlush>();
		services
			.AddOpenTelemetry()
			.WithMetrics(metrics =>
			{
				metrics
					.AddMeter(Meters)
					.AddReader(sp => new PeriodicExportingMetricReader(sp.GetRequiredService<UsageExporter>())
					{
						TemporalityPreference = MetricReaderTemporalityPreference.Delta,
					});
				if (context.OtlpEnabled)
				{
					metrics.AddOtlpExporter();
				}
			});
	}
}

internal class MetricsModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services) => MetricsPipeline.Register(services, ObservabilityContext.Resolve());
}
