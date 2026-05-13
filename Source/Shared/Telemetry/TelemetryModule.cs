using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Shared.DI;
using Shared.Options;

namespace Shared.Telemetry;

public class TelemetryModule : IServiceModule
{
	private const string OtlpEndpointEnvVar = "OTEL_EXPORTER_OTLP_ENDPOINT";

	public void RegisterServices(IServiceCollection services)
	{
		var otlpEnabled = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(OtlpEndpointEnvVar));
		var environment =
			Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
			?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
			?? Environments.Development;
		var version = typeof(TelemetryModule).Assembly.GetName().Version?.ToString() ?? "0.0.0";

		services.AddLogging(b => b.ClearProviders());

		services.AddSingleton<VerbositySampler>();

		services
			.AddOpenTelemetry()
			.ConfigureResource(r =>
				r.AddService(SammlarrTelemetry.ServiceName, serviceVersion: version)
					.AddAttributes([new KeyValuePair<string, object>("deployment.environment", environment)])
			)
			.WithTracing(tracing =>
			{
				tracing.AddSource(SammlarrTelemetry.ServiceName);
				tracing.AddProcessor(new LayerTaggingProcessor());
				if (otlpEnabled)
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
					tracing.AddConsoleExporter();
				}
				tracing.SetSampler<VerbositySampler>();
			})
			.WithMetrics(metrics =>
			{
				metrics.AddMeter(SammlarrTelemetry.MeterName);
				if (otlpEnabled)
				{
					metrics.AddOtlpExporter();
				}
				else
				{
					metrics.AddConsoleExporter();
				}
			})
			.WithLogging(
				logging =>
				{
					logging.AddProcessor(new LogExceptionToActivityProcessor());
					if (otlpEnabled)
					{
						logging.AddOtlpExporter();
					}
					else
					{
						logging.AddConsoleExporter();
					}
				},
				opts =>
				{
					opts.IncludeFormattedMessage = true;
					opts.IncludeScopes = true;
				}
			);

		services
			.AddOptions<LoggerFilterOptions>()
			.Configure<TelemetryOptions>(
				(filterOpts, telemetryOpts) =>
				{
					filterOpts.MinLevel = LogLevel.Warning;
					filterOpts.AddFilter("Sammlarr", MapLogLevel(telemetryOpts.Verbosity));
					filterOpts.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Information);
					filterOpts.AddFilter("Microsoft.AspNetCore", LogLevel.Information);
					filterOpts.AddFilter("Microsoft.Extensions.Http", LogLevel.Warning);
				}
			);
	}

	private static LogLevel MapLogLevel(TelemetryVerbosity verbosity) =>
		verbosity switch
		{
			TelemetryVerbosity.Verbose => LogLevel.Debug,
			TelemetryVerbosity.Normal => LogLevel.Information,
			_ => LogLevel.Warning,
		};
}

internal sealed class VerbositySampler : Sampler
{
	private readonly Sampler _inner;

	public VerbositySampler(TelemetryOptions options)
	{
		_inner = options.Verbosity == TelemetryVerbosity.Quiet ? new AlwaysOffSampler() : new AlwaysOnSampler();
	}

	public override SamplingResult ShouldSample(in SamplingParameters samplingParameters) => _inner.ShouldSample(samplingParameters);
}
