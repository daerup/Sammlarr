using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging.Testing;

namespace Harness;

internal sealed class TelemetryCapture : IDisposable
{
	private readonly ConcurrentQueue<Activity> _spans = new();

	private readonly ActivityListener _listener;

	internal TelemetryCapture()
	{
		_listener = new ActivityListener
		{
			ShouldListenTo = _ => true,
			Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
			ActivityStopped = _spans.Enqueue,
		};
		ActivitySource.AddActivityListener(_listener);
	}

	internal FakeLogCollector Logs { get; } = new();

	internal IEnumerable<string> Written(Func<FakeLogRecord, bool> counted)
	{
		return [.. Logs.GetSnapshot().Where(counted).SelectMany(Texts), .. _spans.SelectMany(Texts)];
	}

	public void Dispose() => _listener.Dispose();

	private static IEnumerable<string> Texts(FakeLogRecord record)
	{
		return [record.Message, .. (record.StructuredState ?? []).Select(pair => pair.Value ?? string.Empty), record.Exception?.ToString() ?? string.Empty];
	}

	private static IEnumerable<string> Texts(Activity span)
	{
		return
		[
			span.DisplayName,
			span.OperationName,
			span.StatusDescription ?? string.Empty,
			.. span.TagObjects.Select(tag => $"{tag.Value}"),
			.. span.Events.SelectMany(spanEvent => spanEvent.Tags.Select(tag => $"{tag.Value}").Append(spanEvent.Name)),
		];
	}
}
