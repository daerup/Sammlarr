using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace E2E.Tests;

internal sealed class CircuitFailureSink(string category, string marker, string traceName, Action<string> capture) : ILoggerProvider
{
	private const int TrailingLines = 15;

	private static readonly string? TraceDirectory = Environment.GetEnvironmentVariable("E2E_RESOURCE_LOG");

	private static readonly Lock TraceGate = new();

	private int _remaining;

	public ILogger CreateLogger(string categoryName) => categoryName.StartsWith(category, StringComparison.Ordinal) ? new Sink(this) : NullLogger.Instance;

	public void Dispose() { }

	private void Observe(string message)
	{
		Trace(message);
		if (message.Contains(marker, StringComparison.Ordinal))
		{
			_remaining = TrailingLines;
			capture(message);
			return;
		}

		if (_remaining > 0)
		{
			_remaining--;
			capture(message);
		}
	}

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
