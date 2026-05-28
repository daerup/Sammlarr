using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace UI.Telemetry.Diagnostics;

public sealed record DiagnosticsLogEntry(
	DateTimeOffset Timestamp,
	LogLevel Level,
	string Category,
	int EventId,
	string? EventName,
	string Message,
	string? Exception,
	string? TraceId,
	string? SpanId,
	IReadOnlyDictionary<string, string?> Attributes
);

public sealed record DiagnosticsSpanEntry(
	string TraceId,
	string SpanId,
	string? ParentSpanId,
	string Name,
	ActivityKind Kind,
	ActivityStatusCode Status,
	string? StatusDescription,
	DateTimeOffset StartTime,
	TimeSpan Duration,
	IReadOnlyDictionary<string, string?> Tags,
	IReadOnlyList<DiagnosticsSpanEvent> Events
);

public sealed record DiagnosticsSpanEvent(string Name, DateTimeOffset Timestamp, IReadOnlyDictionary<string, string?> Tags);

public sealed record DiagnosticsStats(int LogCount, int SpanCount, long EstimatedBytes, DateTimeOffset? Oldest, DateTimeOffset? Newest);
