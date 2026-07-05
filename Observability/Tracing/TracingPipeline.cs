using Microsoft.Extensions.DependencyInjection;
using Observability.Diagnostics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Observability.Tracing;

internal static class TracingPipeline
{
	public static void Register(IServiceCollection services, ObservabilityContext context)
	{
		if (!context.OtlpEnabled)
		{
			services.AddSingleton<ActivityLogProcessor>();
		}

		services
			.AddOpenTelemetry()
			.ConfigureResource(r =>
				r.AddService(Instrumentation.ServiceName, serviceVersion: context.Version)
					.AddAttributes([new KeyValuePair<string, object>("deployment.environment", context.Environment)])
			)
			.WithTracing(tracing =>
			{
				tracing.AddSource(Instrumentation.ServiceName);
				tracing.AddProcessor(sp => sp.GetRequiredService<InMemoryActivityProcessor>());
				if (context.OtlpEnabled)
				{
					tracing.AddOtlpExporter(opts =>
						opts.BatchExportProcessorOptions = new BatchExportActivityProcessorOptions
						{
							MaxQueueSize = 32768,
							MaxExportBatchSize = 4096,
							ScheduledDelayMilliseconds = 1000,
						}
					);
				}
				else
				{
					tracing.AddProcessor<ActivityLogProcessor>();
				}
			});
	}
}
