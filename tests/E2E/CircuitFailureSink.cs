using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace E2E.Tests;

internal sealed partial class CircuitFailureSink(string category, string marker, string traceName, Action<string> capture) : ILoggerProvider
{
	private static readonly string? TraceDirectory = Environment.GetEnvironmentVariable("E2E_RESOURCE_LOG");

	private static readonly Lock TraceGate = new();

	private bool _capturing;

	public ILogger CreateLogger(string categoryName) => categoryName.StartsWith(category, StringComparison.Ordinal) ? new Sink(this) : NullLogger.Instance;

	public void Dispose() { }

	internal static void AssertNone(IReadOnlyCollection<string> crashed)
	{
		if (crashed.Count > 0)
		{
			Assert.Fail($"A circuit crashed:{Environment.NewLine}{string.Join(Environment.NewLine, crashed)}");
		}
	}

	private void Observe(string message)
	{
		Trace(message);
		if (message.Contains(marker, StringComparison.Ordinal))
		{
			_capturing = true;
		}
		else if (LogEntry().IsMatch(message))
		{
			_capturing = false;
		}

		if (_capturing)
		{
			capture(message);
		}
	}

	[GeneratedRegex(@"^\d+: \S+ \[\d{2}:\d{2}:\d{2}\.\d{3} [A-Z]{3}\] ")]
	private static partial Regex LogEntry();

	private void Trace(string message)
	{
		if (TraceDirectory is null)
		{
			return;
		}

		Directory.CreateDirectory(TraceDirectory);
		lock (TraceGate)
		{
			File.AppendAllText(Path.Combine(TraceDirectory, $"{traceName}.log"), $"{message}{Environment.NewLine}");
		}
	}

	private sealed class Sink(CircuitFailureSink owner) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
			owner.Observe(formatter(state, exception));
	}
}
