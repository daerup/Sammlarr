using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Observability.Diagnostics;
using Serilog.Core;
using TestInfrastructure;

namespace Shared.Tests.Observability;

public class LogCaptureServiceTests
{
	private static readonly IReadOnlyDictionary<string, string?> NoAttributes = new Dictionary<string, string?>();

	[Test]
	public async Task Start_WhenCalled_SetsLevelAndActivates()
	{
		var testee = new LogCaptureService(new LoggingLevelSwitch());

		testee.Start(LogLevel.Warning);

		await Assert.That(testee.CaptureLevel).IsEqualTo(LogLevel.Warning);
		await Assert.That(testee.IsCapturing).IsTrue();
		await Assert.That(testee.IsPaused).IsFalse();
		await Assert.That(testee.IsActive).IsTrue();
	}

	[Test]
	public async Task Stop_WhenCalledAfterStart_DeactivatesAndClearsPause()
	{
		var testee = new LogCaptureService(new LoggingLevelSwitch());
		testee.Start(LogLevel.Information);
		testee.Pause();

		testee.Stop();

		await Assert.That(testee.IsCapturing).IsFalse();
		await Assert.That(testee.IsPaused).IsFalse();
		await Assert.That(testee.IsActive).IsFalse();
	}

	[Test]
	public async Task AppendLog_WhenNotCapturing_IsIgnored()
	{
		var testee = new LogCaptureService(new LoggingLevelSwitch());

		testee.AppendLog(MakeLog(LogLevel.Error));

		await Assert.That(testee.SnapshotLogs()).IsEmpty();
	}

	[Test]
	public async Task AppendLog_WhenBelowCaptureLevel_IsIgnored()
	{
		var testee = new LogCaptureService(new LoggingLevelSwitch());
		testee.Start(LogLevel.Warning);

		testee.AppendLog(MakeLog(LogLevel.Debug));
		testee.AppendLog(MakeLog(LogLevel.Information));

		await Assert.That(testee.SnapshotLogs()).IsEmpty();
	}

	[Test]
	public async Task AppendLog_WhenAtOrAboveLevel_IsRecorded()
	{
		var testee = new LogCaptureService(new LoggingLevelSwitch());
		testee.Start(LogLevel.Information);

		testee.AppendLog(MakeLog(LogLevel.Information, "info"));
		testee.AppendLog(MakeLog(LogLevel.Error, "boom"));

		var logs = testee.SnapshotLogs();
		await Assert.That(logs.Count).IsEqualTo(2);
		await Assert.That(logs[0].Message).IsEqualTo("info");
		await Assert.That(logs[1].Message).IsEqualTo("boom");
	}

	[Test]
	public async Task Pause_WhenAppendingLogs_BuffersUntilResume()
	{
		var testee = new LogCaptureService(new LoggingLevelSwitch());
		testee.Start(LogLevel.Trace);

		testee.Pause();
		testee.AppendLog(MakeLog(LogLevel.Information, "ignored"));
		await Assert.That(testee.SnapshotLogs()).IsEmpty();

		testee.Resume();
		testee.AppendLog(MakeLog(LogLevel.Information, "kept"));

		var logs = testee.SnapshotLogs();
		await Assert.That(logs.Count).IsEqualTo(1);
		await Assert.That(logs[0].Message).IsEqualTo("kept");
	}

	[Test]
	public async Task AppendLog_OverBufferSize_DropsOldest()
	{
		var testee = new LogCaptureService(new LoggingLevelSwitch());
		testee.SetBufferSize(10);
		testee.Start(LogLevel.Trace);
		var overflow = 5;

		for (var i = 0; i < testee.BufferSize + overflow; i++)
		{
			testee.AppendLog(MakeLog(LogLevel.Information, i.ToString()));
		}

		var logs = testee.SnapshotLogs();
		await Assert.That(logs.Count).IsEqualTo(testee.BufferSize);
		await Assert.That(logs[0].Message).IsEqualTo(overflow.ToString());
		await Assert.That(logs[^1].Message).IsEqualTo((testee.BufferSize + overflow - 1).ToString());
	}

	[Test]
	public async Task SetBufferSize_WhenShrinking_TrimsOldestEntries()
	{
		var testee = new LogCaptureService(new LoggingLevelSwitch());
		testee.Start(LogLevel.Trace);
		for (var i = 0; i < 50; i++)
		{
			testee.AppendLog(MakeLog(LogLevel.Information, i.ToString()));
		}

		testee.SetBufferSize(10);

		var logs = testee.SnapshotLogs();
		await Assert.That(logs.Count).IsEqualTo(10);
		await Assert.That(logs[0].Message).IsEqualTo("40");
		await Assert.That(logs[^1].Message).IsEqualTo("49");
	}

	[Test]
	public async Task SetBufferSize_WithZero_Throws()
	{
		var testee = new LogCaptureService(new LoggingLevelSwitch());

		await Assert.That(() => testee.SetBufferSize(0)).Throws<ArgumentOutOfRangeException>();
	}

	[Test]
	public async Task Clear_WhenCalled_EmptiesBothQueues()
	{
		var testee = new LogCaptureService(new LoggingLevelSwitch());
		testee.Start(LogLevel.Trace);
		testee.AppendLog(MakeLog(LogLevel.Information));
		testee.AppendSpan(MakeSpan("span.one", DateTimeOffset.UtcNow));

		testee.Clear();

		await Assert.That(testee.SnapshotLogs()).IsEmpty();
		await Assert.That(testee.SnapshotSpans()).IsEmpty();
	}

	[Test]
	public async Task UpdateLevel_WhenCapturing_ChangesFilterImmediately()
	{
		var testee = new LogCaptureService(new LoggingLevelSwitch());
		testee.Start(LogLevel.Information);
		testee.AppendLog(MakeLog(LogLevel.Debug, "before"));
		await Assert.That(testee.SnapshotLogs()).IsEmpty();

		testee.UpdateLevel(LogLevel.Debug);
		testee.AppendLog(MakeLog(LogLevel.Debug, "after"));

		var logs = testee.SnapshotLogs();
		await Assert.That(testee.CaptureLevel).IsEqualTo(LogLevel.Debug);
		await Assert.That(logs.Count).IsEqualTo(1);
		await Assert.That(logs[0].Message).IsEqualTo("after");
	}

	[Test]
	public async Task UpdateLevel_WhenUnchanged_DoesNotRaiseChanged()
	{
		var testee = new LogCaptureService(new LoggingLevelSwitch());
		testee.Start(LogLevel.Information);
		var count = 0;
		testee.Changed += () => Interlocked.Increment(ref count);

		testee.UpdateLevel(LogLevel.Information);

		await Assert.That(count).IsEqualTo(0);
	}

	[Test]
	public async Task Changed_FiresOnLifecycleAndAppend()
	{
		var testee = new LogCaptureService(new LoggingLevelSwitch());
		var count = 0;
		testee.Changed += () => Interlocked.Increment(ref count);

		testee.Start(LogLevel.Trace);
		testee.AppendLog(MakeLog(LogLevel.Information));
		testee.Pause();
		testee.Resume();
		testee.Clear();
		testee.Stop();

		await Assert.That(count).IsGreaterThanOrEqualTo(6);
	}

	[Test]
	public async Task AppendSpan_WhenNotCapturing_IsIgnored()
	{
		var testee = new LogCaptureService(new LoggingLevelSwitch());

		testee.AppendSpan(MakeSpan("ignored", DateTimeOffset.UtcNow));

		await Assert.That(testee.SnapshotSpans()).IsEmpty();
	}

	[Test]
	public async Task AppendSpan_WhenActive_IsRecorded()
	{
		var testee = new LogCaptureService(new LoggingLevelSwitch());
		testee.Start(LogLevel.Trace);

		testee.AppendSpan(MakeSpan("span.one", DateTimeOffset.UtcNow));

		var spans = testee.SnapshotSpans();
		await Assert.That(spans.Count).IsEqualTo(1);
		await Assert.That(spans[0].Name).IsEqualTo("span.one");
	}

	[Test]
	public async Task AppendSpan_WhenPaused_IsIgnored()
	{
		var testee = new LogCaptureService(new LoggingLevelSwitch());
		testee.Start(LogLevel.Trace);
		testee.Pause();

		testee.AppendSpan(MakeSpan("ignored", DateTimeOffset.UtcNow));

		await Assert.That(testee.SnapshotSpans()).IsEmpty();
	}

	[Test]
	public async Task AppendSpan_OverBufferSize_DropsOldest()
	{
		var testee = new LogCaptureService(new LoggingLevelSwitch());
		testee.SetBufferSize(5);
		testee.Start(LogLevel.Trace);

		for (var i = 0; i < 8; i++)
		{
			testee.AppendSpan(MakeSpan($"span.{i}", DateTimeOffset.UtcNow));
		}

		var spans = testee.SnapshotSpans();
		await Assert.That(spans.Count).IsEqualTo(5);
		await Assert.That(spans[0].Name).IsEqualTo("span.3");
		await Assert.That(spans[^1].Name).IsEqualTo("span.7");
	}

	[Test]
	public async Task GetStats_WhenNothingWasCaptured_HasNoWindow()
	{
		var stats = new LogCaptureService(new LoggingLevelSwitch()).GetStats();

		await Assert.That(stats.Oldest).IsNone();
		await Assert.That(stats.Newest).IsNone();
	}

	[Test]
	public async Task GetStats_WhenLogsAndSpansPresent_ReportsCountsAndWindow()
	{
		var testee = new LogCaptureService(new LoggingLevelSwitch());
		testee.Start(LogLevel.Trace);
		var first = DateTimeOffset.UtcNow.AddMinutes(-5);
		var last = DateTimeOffset.UtcNow;
		testee.AppendLog(MakeLog(LogLevel.Information, "first", first));
		testee.AppendLog(MakeLog(LogLevel.Information, "last", last));
		var earliest = first.AddMinutes(-1);
		testee.AppendSpan(MakeSpan("span.one", earliest));

		var stats = testee.GetStats();

		await Assert.That(stats.LogCount).IsEqualTo(2);
		await Assert.That(stats.SpanCount).IsEqualTo(1);
		await Assert.That(stats.Oldest).IsSome().And.IsEqualTo(earliest);
		await Assert.That(stats.Newest).IsSome().And.IsEqualTo(last);
		await Assert.That(stats.EstimatedBytes).IsGreaterThan(0);
	}

	[Test]
	public async Task ExportJsonLines_WhenInvoked_ContainsLogsAsJsonl()
	{
		var testee = new LogCaptureService(new LoggingLevelSwitch());
		testee.Start(LogLevel.Trace);
		testee.AppendLog(MakeLog(LogLevel.Information, "first"));
		testee.AppendLog(MakeLog(LogLevel.Error, "boom"));

		using var stream = testee.ExportJsonLines();
		using var reader = new StreamReader(stream);
		var text = await reader.ReadToEndAsync();

		var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
		await Assert.That(lines.Length).IsEqualTo(2);
		await Assert.That(lines[0]).Contains("first");
		await Assert.That(lines[1]).Contains("boom");
	}

	[Test]
	public async Task ExportPerfettoTrace_WhenSpansPresent_EmitsTraceEvents()
	{
		var testee = new LogCaptureService(new LoggingLevelSwitch());
		testee.Start(LogLevel.Trace);
		var start = DateTimeOffset.UtcNow;
		testee.AppendSpan(MakeSpan("root", start, TimeSpan.FromMilliseconds(100)));
		testee.AppendSpan(MakeSpan("child", start.AddMilliseconds(10), TimeSpan.FromMilliseconds(50)));

		using var stream = testee.ExportPerfettoTrace();
		using var doc = await JsonDocument.ParseAsync(stream);

		var events = doc.RootElement.GetProperty("traceEvents");
		await Assert.That(events.GetArrayLength()).IsGreaterThanOrEqualTo(2);
		var spanNames = events.EnumerateArray().Where(e => e.GetProperty("ph").GetString() == "X").Select(e => e.GetProperty("name").GetString()).ToList();
		await Assert.That(spanNames).Contains("root");
		await Assert.That(spanNames).Contains("child");
	}

	[Test]
	public async Task ExportZip_WhenInvoked_ContainsLogsAndTraceEntries()
	{
		var testee = new LogCaptureService(new LoggingLevelSwitch());
		testee.Start(LogLevel.Trace);
		testee.AppendLog(MakeLog(LogLevel.Information, "msg"));
		testee.AppendSpan(MakeSpan("span.one", DateTimeOffset.UtcNow));

		using var stream = testee.ExportZip();
		using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

		var logsEntry = archive.GetEntry("logs.jsonl");
		var traceEntry = archive.GetEntry("trace.json");
		await Assert.That(logsEntry).IsNotNull();
		await Assert.That(traceEntry).IsNotNull();

		using var logsReader = new StreamReader(logsEntry!.Open());
		var logsText = await logsReader.ReadToEndAsync();
		await Assert.That(logsText).Contains("msg");

		using var traceReader = new StreamReader(traceEntry!.Open());
		var traceText = await traceReader.ReadToEndAsync();
		await Assert.That(traceText).Contains("traceEvents");
		await Assert.That(traceText).Contains("span.one");
	}

	private static DiagnosticsLogEntry MakeLog(LogLevel level, string message = "msg", DateTimeOffset? timestamp = null) =>
		new(timestamp ?? DateTimeOffset.UtcNow, level, "TestCategory", 0, null, message, null, null, null, NoAttributes);

	private static DiagnosticsSpanEntry MakeSpan(string name, DateTimeOffset start, TimeSpan? duration = null) =>
		new(
			"0123456789abcdef0123456789abcdef",
			"0123456789abcdef",
			null,
			name,
			ActivityKind.Internal,
			ActivityStatusCode.Unset,
			null,
			start,
			duration ?? TimeSpan.FromMilliseconds(1),
			NoAttributes,
			[]
		);
}
