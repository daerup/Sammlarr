using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace E2E.Tests.PlexFree;

internal sealed partial class SetupCodeSink(string category) : ILoggerProvider
{
	private TaskCompletionSource<string> _code = new(TaskCreationOptions.RunContinuationsAsynchronously);

	internal Task<string> NextCodeAsync() => _code.Task;

	internal void Forget() => _code = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

	public ILogger CreateLogger(string categoryName) => categoryName.StartsWith(category, StringComparison.Ordinal) ? new Sink(this) : NullLogger.Instance;

	public void Dispose() { }

	private void Observe(string message)
	{
		var match = SetupCode().Match(message);
		if (match.Success)
		{
			_code.TrySetResult(match.Groups[1].Value);
		}
	}

	[GeneratedRegex(@"setup code ""?([2-9A-Z]{4}-[2-9A-Z]{4})")]
	private static partial Regex SetupCode();

	private sealed class Sink(SetupCodeSink owner) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			owner.Observe(formatter(state, exception));
		}
	}
}
