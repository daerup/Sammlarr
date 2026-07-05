using System.Diagnostics;
using Serilog;
using Observability.Logging;

namespace Shared.Tests.Observability;

public class ActivityStatusSinkTests
{
	[Test]
	public async Task ExceptionLog_TagsCurrentActivityWithErrorStatus()
	{
		using var listener = NewActivityListener();
		ActivitySource.AddActivityListener(listener);
		using var source = new ActivitySource("test.logbridge.error");
		using var logger = new LoggerConfiguration().WriteTo.Sink(new ActivityStatusSink()).CreateLogger();

		using var activity = source.StartActivity("op");
		logger.Error(new InvalidOperationException("boom"), "failed");

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
		using var logger = new LoggerConfiguration().WriteTo.Sink(new ActivityStatusSink()).CreateLogger();

		using var activity = source.StartActivity("op");
		logger.Information("nothing wrong");

		await Assert.That(activity!.Status).IsEqualTo(ActivityStatusCode.Unset);
		await Assert.That(activity.Events.Any(e => e.Name == "exception")).IsFalse();
	}

	private static ActivityListener NewActivityListener() => new() { ShouldListenTo = _ => true, Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded };
}
