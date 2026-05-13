using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Telemetry;

namespace Shared.Tests.Unit.Telemetry;

public class ActivityLogProcessorTests
{
	[Test]
	public async Task OnEnd_LogsCompletedSammlarrActivity()
	{
		var sink = new CapturingLoggerProvider();
		using var loggerFactory = LoggerFactory.Create(b => b.AddProvider(sink).SetMinimumLevel(LogLevel.Information));
		var processor = new ActivityLogProcessor(loggerFactory);

		using var listener = AllSourcesListener();
		ActivitySource.AddActivityListener(listener);
		using var source = new ActivitySource(SammlarrTelemetry.ServiceName);
		using var activity = source.StartActivity("test.op");
		activity!.DisplayName = "TEST RUN";
		activity.Stop();

		processor.OnEnd(activity);

		await Assert.That(sink.Entries).Count().IsGreaterThan(0);
		await Assert.That(sink.Entries.Last().Message).Contains("TEST RUN");
	}

	[Test]
	public async Task OnEnd_IgnoresActivitiesFromOtherSources()
	{
		var sink = new CapturingLoggerProvider();
		using var loggerFactory = LoggerFactory.Create(b => b.AddProvider(sink).SetMinimumLevel(LogLevel.Information));
		var processor = new ActivityLogProcessor(loggerFactory);

		using var listener = AllSourcesListener();
		ActivitySource.AddActivityListener(listener);
		using var source = new ActivitySource("Not.Sammlarr");
		using var activity = source.StartActivity("noise");
		activity!.Stop();

		processor.OnEnd(activity);

		await Assert.That(sink.Entries).IsEmpty();
	}

	private static ActivityListener AllSourcesListener() =>
		new() { ShouldListenTo = _ => true, Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded };

	private sealed class CapturingLoggerProvider : ILoggerProvider
	{
		public List<(string Category, string Message)> Entries { get; } = [];

		public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Entries);

		public void Dispose() { }

		private sealed class CapturingLogger(string category, List<(string Category, string Message)> entries) : ILogger
		{
			public IDisposable? BeginScope<TState>(TState state)
				where TState : notnull => null;

			public bool IsEnabled(LogLevel logLevel) => true;

			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
			{
				entries.Add((category, formatter(state, exception)));
			}
		}
	}
}
