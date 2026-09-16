using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Polly.Telemetry;

namespace API.Tests;

public class ResilienceTelemetryLoggingTests
{
	[Test]
	public async Task Retry_SurfacesOnRetry_AtWarning_WhilePipelineExecuted_StaysDebug()
	{
		var sink = new CapturingLoggerProvider();
		var telemetry = new TelemetryOptions
		{
			LoggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(sink)),
			SeverityProvider = args => ResilienceModule.Severity(args.Event.EventName, args.Event.Severity),
		};

		var pipeline = new ResiliencePipelineBuilder<string>()
			.AddRetry(
				new RetryStrategyOptions<string>
				{
					ShouldHandle = args => new ValueTask<bool>(args.Outcome.Result == "transient"),
					MaxRetryAttempts = 1,
					Delay = TimeSpan.Zero,
				}
			)
			.ConfigureTelemetry(telemetry)
			.Build();

		var attempt = 0;
		await pipeline.ExecuteAsync(_ => new ValueTask<string>(attempt++ == 0 ? "transient" : "ok"));

		await Assert.That(sink.Entries.Any(entry => entry.Message.Contains("OnRetry"))).IsTrue();
		var onRetry = sink.Entries.Single(entry => entry.Message.Contains("OnRetry"));
		await Assert.That(onRetry.Level).IsEqualTo(LogLevel.Warning);

		await Assert.That(sink.Entries.Any(entry => entry.Message.Contains("pipeline executed"))).IsTrue();
		var pipelineExecuted = sink.Entries.First(entry => entry.Message.Contains("pipeline executed"));
		await Assert.That(pipelineExecuted.Level).IsEqualTo(LogLevel.Debug);
	}

	private sealed class CapturingLoggerProvider : ILoggerProvider
	{
		public List<(LogLevel Level, string Message)> Entries { get; } = [];

		public ILogger CreateLogger(string categoryName) => new CapturingLogger(Entries);

		public void Dispose() { }

		private sealed class CapturingLogger(List<(LogLevel, string)> entries) : ILogger
		{
			public IDisposable? BeginScope<TState>(TState state)
				where TState : notnull => null;

			public bool IsEnabled(LogLevel logLevel) => true;

			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
				entries.Add((logLevel, formatter(state, exception)));
		}
	}
}
