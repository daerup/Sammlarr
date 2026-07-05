using Observability.Tracing;
using System.Diagnostics;
using OpenTelemetry;

namespace Observability.Diagnostics;

public sealed class InMemoryActivityProcessor(LogCaptureService capture) : BaseProcessor<Activity>
{
	public override void OnEnd(Activity data)
	{
		if (data.Source.Name != Instrumentation.ServiceName)
		{
			return;
		}
		if (data.OperationName.StartsWith("blazor.", StringComparison.Ordinal))
		{
			return;
		}

		var tags = new Dictionary<string, string?>(StringComparer.Ordinal);
		foreach (var (key, value) in data.TagObjects)
		{
			tags[key] = value?.ToString();
		}

		var events = new List<DiagnosticsSpanEvent>();
		foreach (var ev in data.Events)
		{
			var evTags = new Dictionary<string, string?>(StringComparer.Ordinal);
			foreach (var (key, value) in ev.Tags)
			{
				evTags[key] = value?.ToString();
			}
			events.Add(new DiagnosticsSpanEvent(ev.Name, ev.Timestamp, evTags));
		}

		var entry = new DiagnosticsSpanEntry(
			data.TraceId.ToString(),
			data.SpanId.ToString(),
			data.ParentSpanId == default ? null : data.ParentSpanId.ToString(),
			data.DisplayName,
			data.Kind,
			data.Status,
			data.StatusDescription,
			data.StartTimeUtc,
			data.Duration,
			tags,
			events
		);

		capture.AppendSpan(entry);
	}
}
