using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace E2E.Tests;

internal sealed class CircuitFailureSink(string category, string marker, Action<string> capture) : ILoggerProvider
{
	private const int TrailingLines = 15;

	private int _remaining;

	public ILogger CreateLogger(string categoryName) => categoryName.StartsWith(category, StringComparison.Ordinal) ? new Sink(this) : NullLogger.Instance;

	public void Dispose() { }

	private void Observe(string message)
	{
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

	private sealed class Sink(CircuitFailureSink owner) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
			owner.Observe(formatter(state, exception));
	}
}
