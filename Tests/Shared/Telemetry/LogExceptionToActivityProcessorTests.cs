using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Shared.Telemetry;

namespace Shared.Tests.Telemetry;

public class LogExceptionToActivityProcessorTests
{
	[Test]
	public async Task ExceptionLog_TagsCurrentActivityWithErrorStatus()
	{
		using var listener = NewActivityListener();
		ActivitySource.AddActivityListener(listener);
		using var source = new ActivitySource("test.logbridge.error");
		using var loggerFactory = NewLoggerFactory();
		var logger = loggerFactory.CreateLogger("test");

		using var activity = source.StartActivity("op");
		logger.LogError(new InvalidOperationException("boom"), "failed");

		await Assert.That(activity!.Status).IsEqualTo(ActivityStatusCode.Error);
		await Assert.That(activity.StatusDescription).IsEqualTo("boom");
		await Assert.That(activity.Events.Any(e => e.Name == "exception")).IsTrue();
	}

	[Test]
	public async Task LogWithoutException_LeavesActivityUnset()
	{
		using var listener = NewActivityListener();
		ActivitySource.AddActivityListener(listener);
		using var source = new ActivitySource("test.logbridge.noexception");
		using var loggerFactory = NewLoggerFactory();
		var logger = loggerFactory.CreateLogger("test");

		using var activity = source.StartActivity("op");
		logger.LogInformation("nothing wrong");

		await Assert.That(activity!.Status).IsEqualTo(ActivityStatusCode.Unset);
		await Assert.That(activity.Events.Any(e => e.Name == "exception")).IsFalse();
	}

	private static ActivityListener NewActivityListener() =>
		new() { ShouldListenTo = _ => true, Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded };

	private static ILoggerFactory NewLoggerFactory() =>
		LoggerFactory.Create(builder =>
			builder.AddOpenTelemetry(options =>
			{
				options.AddProcessor(new LogExceptionToActivityProcessor());
				options.IncludeFormattedMessage = true;
			})
		);
}
