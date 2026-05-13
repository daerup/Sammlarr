using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
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
		var telemetryOptions = ResolveTelemetryOptions(services);
		var otlpEnabled = ShouldUseOtlp(telemetryOptions, Environment.GetEnvironmentVariable(OtlpEndpointEnvVar));
		var environment =
			Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
			?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
			?? Environments.Development;
		var version = typeof(TelemetryModule).Assembly.GetName().Version?.ToString() ?? "0.0.0";

		services.AddLogging(b =>
		{
			b.ClearProviders();
			if (!otlpEnabled)
			{
				b.AddConsole(opts => opts.FormatterName = StructuredConsoleFormatter.FormatterName);
				b.AddConsoleFormatter<StructuredConsoleFormatter, ConsoleFormatterOptions>();
			}
		});

		if (!otlpEnabled)
		{
			services.AddSingleton<ActivityLogProcessor>();
		}

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
					tracing.AddProcessor<ActivityLogProcessor>();
				}
			})
			.WithMetrics(metrics =>
			{
				metrics.AddMeter(SammlarrTelemetry.MeterName);
				if (otlpEnabled)
				{
					metrics.AddOtlpExporter();
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
					var frameworkLevel = telemetryOpts.Verbosity == TelemetryVerbosity.Verbose ? LogLevel.Debug : LogLevel.Warning;
					filterOpts.AddFilter("Microsoft.EntityFrameworkCore", frameworkLevel);
					filterOpts.AddFilter("Microsoft.AspNetCore", frameworkLevel);
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

	internal static bool ShouldUseOtlp(TelemetryOptions options, string? otlpEndpoint) =>
		options.Exporter == TelemetryExporter.Auto && !string.IsNullOrWhiteSpace(otlpEndpoint);

	private static TelemetryOptions ResolveTelemetryOptions(IServiceCollection services)
	{
		using var sp = services.BuildServiceProvider();
		return sp.GetRequiredService<TelemetryOptions>();
	}
}
