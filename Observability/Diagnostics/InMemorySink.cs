using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;

namespace Observability.Diagnostics;

public sealed class InMemorySink(LogCaptureService capture) : ILogEventSink
{
	public void Emit(LogEvent logEvent)
	{
		if (!capture.IsActive)
		{
			return;
		}

		var level = MapLevel(logEvent.Level);
		if (level < capture.CaptureLevel)
		{
			return;
		}

		var activity = Activity.Current;
		var entry = new DiagnosticsLogEntry(
			logEvent.Timestamp,
			level,
			SourceContext(logEvent),
			0,
			null,
			logEvent.RenderMessage(),
			logEvent.Exception?.ToString(),
			activity?.TraceId.ToString(),
			activity?.SpanId.ToString(),
			ExtractAttributes(logEvent)
		);

		capture.AppendLog(entry);
	}

	private static string SourceContext(LogEvent logEvent) =>
		logEvent.Properties.TryGetValue("SourceContext", out var value) && value is ScalarValue { Value: string context } ? context : string.Empty;

	private static IReadOnlyDictionary<string, string?> ExtractAttributes(LogEvent logEvent)
	{
		var result = new Dictionary<string, string?>(logEvent.Properties.Count, StringComparer.Ordinal);
		foreach (var (key, value) in logEvent.Properties)
		{
			if (key == "SourceContext")
			{
				continue;
			}
			result[key] = value is ScalarValue scalar ? scalar.Value?.ToString() : value.ToString();
		}
		return result;
	}

	private static LogLevel MapLevel(LogEventLevel level) =>
		level switch
		{
			LogEventLevel.Verbose => LogLevel.Trace,
			LogEventLevel.Debug => LogLevel.Debug,
			LogEventLevel.Information => LogLevel.Information,
			LogEventLevel.Warning => LogLevel.Warning,
			LogEventLevel.Error => LogLevel.Error,
			LogEventLevel.Fatal => LogLevel.Critical,
		};
}
