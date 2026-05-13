using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace UI.Telemetry.Diagnostics;

[ProviderAlias("InMemoryDiagnostics")]
public sealed class InMemoryLoggerProvider(LogCaptureService capture) : ILoggerProvider
{
	private readonly ConcurrentDictionary<string, InMemoryLogger> _loggers = new(StringComparer.Ordinal);

	public ILogger CreateLogger(string categoryName) => _loggers.GetOrAdd(categoryName, name => new InMemoryLogger(capture, name));

	public void Dispose() => _loggers.Clear();

	private sealed class InMemoryLogger(LogCaptureService capture, string category) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => capture.IsActive && logLevel >= capture.CaptureLevel;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			if (!IsEnabled(logLevel))
			{
				return;
			}

			var activity = Activity.Current;
			var attributes = ExtractAttributes(state);

			var entry = new DiagnosticsLogEntry(
				DateTimeOffset.UtcNow,
				logLevel,
				category,
				eventId.Id,
				string.IsNullOrEmpty(eventId.Name) ? null : eventId.Name,
				formatter(state, exception),
				exception?.ToString(),
				activity?.TraceId.ToString(),
				activity?.SpanId.ToString(),
				attributes
			);

			capture.AppendLog(entry);
		}

		private static IReadOnlyDictionary<string, string?> ExtractAttributes<TState>(TState state)
		{
			if (state is not IReadOnlyList<KeyValuePair<string, object?>> kvps)
			{
				return EmptyAttributes;
			}

			var result = new Dictionary<string, string?>(kvps.Count, StringComparer.Ordinal);
			foreach (var kvp in kvps)
			{
				if (kvp.Key == "{OriginalFormat}")
				{
					continue;
				}
				result[kvp.Key] = kvp.Value?.ToString();
			}
			return result;
		}

		private static readonly IReadOnlyDictionary<string, string?> EmptyAttributes = new Dictionary<string, string?>();
	}
}
