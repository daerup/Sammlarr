using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;

namespace Observability.Diagnostics;

public sealed class LogCaptureService(LoggingLevelSwitch appLevel)
{
	public const int DefaultBufferSize = 1_000;
	public static IReadOnlyList<int> BufferSizeOptions { get; } = [1_000, 5_000, 10_000, 25_000, 50_000, 100_000];

	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

	private readonly LogEventLevel _baseline = appLevel.MinimumLevel;
	private readonly ConcurrentQueue<DiagnosticsLogEntry> _logs = new();
	private readonly ConcurrentQueue<DiagnosticsSpanEntry> _spans = new();

	public LogLevel CaptureLevel { get; private set; } = LogLevel.Information;
	public int BufferSize { get; private set; } = DefaultBufferSize;
	public bool IsCapturing { get; private set; }
	public bool IsPaused { get; private set; }

	public event Action? Changed;

	public bool IsActive => IsCapturing && !IsPaused;

	public void Start(LogLevel level)
	{
		CaptureLevel = level;
		IsCapturing = true;
		IsPaused = false;
		appLevel.MinimumLevel = Deepest(_baseline, ToSerilog(level));
		RaiseChanged();
	}

	public void UpdateLevel(LogLevel level)
	{
		if (CaptureLevel == level)
		{
			return;
		}
		CaptureLevel = level;
		appLevel.MinimumLevel = Deepest(_baseline, ToSerilog(level));
		RaiseChanged();
	}

	public void SetBufferSize(int size)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
		if (BufferSize == size)
		{
			return;
		}
		BufferSize = size;
		Trim();
		RaiseChanged();
	}

	public void Stop()
	{
		IsCapturing = false;
		IsPaused = false;
		appLevel.MinimumLevel = _baseline;
		RaiseChanged();
	}

	private static LogEventLevel Deepest(LogEventLevel first, LogEventLevel second) => first < second ? first : second;

	private static LogEventLevel ToSerilog(LogLevel level) =>
		level switch
		{
			LogLevel.Trace => LogEventLevel.Verbose,
			LogLevel.Debug => LogEventLevel.Debug,
			LogLevel.Information => LogEventLevel.Information,
			LogLevel.Warning => LogEventLevel.Warning,
			LogLevel.Error => LogEventLevel.Error,
			LogLevel.Critical => LogEventLevel.Fatal,
			LogLevel.None => LogEventLevel.Fatal,
		};

	public void Pause()
	{
		if (!IsCapturing)
		{
			return;
		}
		IsPaused = true;
		RaiseChanged();
	}

	public void Resume()
	{
		if (!IsCapturing)
		{
			return;
		}
		IsPaused = false;
		RaiseChanged();
	}

	public void Clear()
	{
		_logs.Clear();
		_spans.Clear();
		RaiseChanged();
	}

	internal void AppendLog(DiagnosticsLogEntry entry)
	{
		if (!IsActive || entry.Level < CaptureLevel)
		{
			return;
		}
		_logs.Enqueue(entry);
		TrimQueue(_logs);
		RaiseChanged();
	}

	internal void AppendSpan(DiagnosticsSpanEntry entry)
	{
		if (!IsActive)
		{
			return;
		}
		_spans.Enqueue(entry);
		TrimQueue(_spans);
		RaiseChanged();
	}

	public IReadOnlyList<DiagnosticsLogEntry> SnapshotLogs() => _logs.ToArray();

	public IReadOnlyList<DiagnosticsSpanEntry> SnapshotSpans() => _spans.ToArray();

	public DiagnosticsStats GetStats()
	{
		var logs = _logs.ToArray();
		var spans = _spans.ToArray();
		var bytes = EstimateBytes(logs) + EstimateBytes(spans);

		var times = logs.Select(log => (DateTimeOffset?)log.Timestamp).Concat(spans.Select(span => (DateTimeOffset?)span.StartTime));
		return new DiagnosticsStats(logs.Length, spans.Length, bytes, Optional.Of(times.Min()), Optional.Of(times.Max()));
	}

	public Stream ExportJsonLines()
	{
		var logs = _logs.ToArray();
		var output = new MemoryStream();
		using (var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true))
		{
			foreach (var log in logs)
			{
				writer.WriteLine(JsonSerializer.Serialize(log, JsonOptions));
			}
		}
		output.Position = 0;
		return output;
	}

	public Stream ExportPerfettoTrace()
	{
		var spans = _spans.ToArray();
		var output = new MemoryStream();
		using (var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = false }))
		{
			writer.WriteStartObject();
			writer.WriteStartArray("traceEvents");

			writer.WriteStartObject();
			writer.WriteString("name", "process_name");
			writer.WriteString("ph", "M");
			writer.WriteNumber("pid", 1);
			writer.WriteStartObject("args");
			writer.WriteString("name", "Sammlarr");
			writer.WriteEndObject();
			writer.WriteEndObject();

			var tidByTrace = new Dictionary<string, int>(StringComparer.Ordinal);
			var nextTid = 1;
			foreach (var span in spans)
			{
				if (!tidByTrace.TryGetValue(span.TraceId, out var tid))
				{
					tid = nextTid++;
					tidByTrace[span.TraceId] = tid;

					writer.WriteStartObject();
					writer.WriteString("name", "thread_name");
					writer.WriteString("ph", "M");
					writer.WriteNumber("pid", 1);
					writer.WriteNumber("tid", tid);
					writer.WriteStartObject("args");
					writer.WriteString("name", $"Trace {span.TraceId.AsSpan(0, Math.Min(8, span.TraceId.Length))}");
					writer.WriteEndObject();
					writer.WriteEndObject();
				}

				writer.WriteStartObject();
				writer.WriteString("name", span.Name);
				writer.WriteString("cat", span.Kind.ToString());
				writer.WriteString("ph", "X");
				writer.WriteNumber("ts", ToUnixMicroseconds(span.StartTime));
				writer.WriteNumber("dur", Math.Max(0L, (long)span.Duration.TotalMicroseconds));
				writer.WriteNumber("pid", 1);
				writer.WriteNumber("tid", tid);
				writer.WriteStartObject("args");
				writer.WriteString("traceId", span.TraceId);
				writer.WriteString("spanId", span.SpanId);
				if (span.ParentSpanId is not null)
				{
					writer.WriteString("parentSpanId", span.ParentSpanId);
				}
				writer.WriteString("kind", span.Kind.ToString());
				writer.WriteString("status", span.Status.ToString());
				if (span.StatusDescription is not null)
				{
					writer.WriteString("statusDescription", span.StatusDescription);
				}
				foreach (var (key, value) in span.Tags)
				{
					writer.WriteString($"tag.{key}", value);
				}
				writer.WriteEndObject();
				writer.WriteEndObject();

				foreach (var ev in span.Events)
				{
					writer.WriteStartObject();
					writer.WriteString("name", ev.Name);
					writer.WriteString("cat", "event");
					writer.WriteString("ph", "i");
					writer.WriteString("s", "t");
					writer.WriteNumber("ts", ToUnixMicroseconds(ev.Timestamp));
					writer.WriteNumber("pid", 1);
					writer.WriteNumber("tid", tid);
					writer.WriteStartObject("args");
					foreach (var (key, value) in ev.Tags)
					{
						writer.WriteString(key, value);
					}
					writer.WriteEndObject();
					writer.WriteEndObject();
				}
			}

			writer.WriteEndArray();
			writer.WriteEndObject();
		}
		output.Position = 0;
		return output;
	}

	public Stream ExportZip()
	{
		var output = new MemoryStream();
		using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
		{
			var logsEntry = archive.CreateEntry("logs.jsonl", CompressionLevel.Optimal);
			using (var logsStream = logsEntry.Open())
			using (var logsWriter = new StreamWriter(logsStream, new UTF8Encoding(false)))
			{
				foreach (var log in _logs.ToArray())
				{
					logsWriter.WriteLine(JsonSerializer.Serialize(log, JsonOptions));
				}
			}

			var traceEntry = archive.CreateEntry("trace.json", CompressionLevel.Optimal);
			using var perfetto = ExportPerfettoTrace();
			using var traceStream = traceEntry.Open();
			perfetto.CopyTo(traceStream);
		}
		output.Position = 0;
		return output;
	}

	private static long ToUnixMicroseconds(DateTimeOffset value) => (long)(value - DateTimeOffset.UnixEpoch).TotalMicroseconds;

	private static long EstimateBytes(IReadOnlyCollection<DiagnosticsLogEntry> logs)
	{
		long total = 0;
		foreach (var log in logs)
		{
			total += 96 + (log.Category.Length + log.Message.Length + (log.Exception?.Length ?? 0)) * sizeof(char);
			foreach (var (key, value) in log.Attributes)
			{
				total += 48 + (key.Length + (value?.Length ?? 0)) * sizeof(char);
			}
		}
		return total;
	}

	private static long EstimateBytes(IReadOnlyCollection<DiagnosticsSpanEntry> spans)
	{
		long total = 0;
		foreach (var span in spans)
		{
			total +=
				160
				+ (span.TraceId.Length + span.SpanId.Length + (span.ParentSpanId?.Length ?? 0) + span.Name.Length + (span.StatusDescription?.Length ?? 0))
					* sizeof(char);
			foreach (var (key, value) in span.Tags)
			{
				total += 48 + (key.Length + (value?.Length ?? 0)) * sizeof(char);
			}
			foreach (var ev in span.Events)
			{
				total += 64 + ev.Name.Length * sizeof(char);
				foreach (var (key, value) in ev.Tags)
				{
					total += 48 + (key.Length + (value?.Length ?? 0)) * sizeof(char);
				}
			}
		}
		return total;
	}

	private void Trim()
	{
		TrimQueue(_logs);
		TrimQueue(_spans);
	}

	private void TrimQueue<T>(ConcurrentQueue<T> queue)
	{
		while (queue.Count > BufferSize && queue.TryDequeue(out _)) { }
	}

	private void RaiseChanged() => Changed?.Invoke();
}
