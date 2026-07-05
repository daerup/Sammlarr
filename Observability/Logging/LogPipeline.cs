using Microsoft.Extensions.DependencyInjection;
using Observability.Tracing;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using Shared.Options;

namespace Observability.Logging;

internal static class LogPipeline
{
	private const string ConsoleTemplate = "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}";
	private static readonly string[] AppLogCategories = ["Sammlarr", "Core", "DB", "API", "UI", "Shared", "Domain", "Contracts"];

	public static void Configure(LoggerConfiguration logger, IServiceProvider provider, ObservabilityContext context)
	{
		var appLevelSwitch = provider.GetRequiredService<LoggingLevelSwitch>();
		var baseline = ObservabilityContext.MapLevel(provider.GetRequiredService<ObservabilityOptions>().LogLevel);

		logger
			.MinimumLevel.Warning()
			.MinimumLevel.Override("Polly", LogEventLevel.Error)
			.Enrich.FromLogContext()
			.Destructure.With(new UnionDestructuringPolicy())
			.Destructure.With(new SensitiveDataDestructuringPolicy())
			.Destructure.ToMaximumCollectionCount(50)
			.Destructure.ToMaximumStringLength(512);

		foreach (var category in AppLogCategories)
		{
			logger.MinimumLevel.Override(category, appLevelSwitch);
		}

		logger.WriteTo.Sink(new ActivityStatusSink());
		logger.WriteTo.Logger(exporter => ConfigureExporters(exporter, context), restrictedToMinimumLevel: baseline);

		foreach (var sink in provider.GetServices<ILogEventSink>())
		{
			logger.WriteTo.Sink(sink);
		}
	}

	private static void ConfigureExporters(LoggerConfiguration exporter, ObservabilityContext context)
	{
		if (context.OtlpEnabled)
		{
			exporter.WriteTo.OpenTelemetry(otlp =>
				otlp.ResourceAttributes = new Dictionary<string, object>
				{
					["service.name"] = Instrumentation.ServiceName,
					["service.version"] = context.Version,
					["deployment.environment"] = context.Environment,
				}
			);
		}
		exporter.WriteTo.Console(outputTemplate: ConsoleTemplate, theme: Console.IsOutputRedirected ? ConsoleTheme.None : AnsiConsoleTheme.Code);
	}
}
