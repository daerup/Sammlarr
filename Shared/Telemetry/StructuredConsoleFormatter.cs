using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace Shared.Telemetry;

internal sealed class StructuredConsoleFormatter : ConsoleFormatter
{
	internal const string FormatterName = "structured";

	private const char Esc = (char)0x1B;
	private const int CategoryWidth = 32;

	private static readonly string Reset = $"{Esc}[0m";
	private static readonly string Gray = $"{Esc}[90m";

	public StructuredConsoleFormatter()
		: base(FormatterName) { }

	public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
	{
		var message = logEntry.Formatter(logEntry.State, logEntry.Exception);
		if (string.IsNullOrEmpty(message) && logEntry.Exception is null)
		{
			return;
		}

		textWriter.Write(Gray);
		textWriter.Write(DateTimeOffset.Now.ToString("HH:mm:ss.fff"));
		textWriter.Write(Reset);
		textWriter.Write(' ');

		textWriter.Write(LevelTag(logEntry.LogLevel));
		textWriter.Write(' ');

		textWriter.Write(Gray);
		textWriter.Write(ShortenCategory(logEntry.Category));
		textWriter.Write(Reset);
		textWriter.Write(' ');

		textWriter.Write(Compact(message));

		WriteTrace(textWriter);

		if (logEntry.Exception is not null)
		{
			textWriter.WriteLine();
			textWriter.Write(logEntry.Exception);
		}

		textWriter.WriteLine();
	}

	private static string LevelTag(LogLevel level) =>
		level switch
		{
			LogLevel.Trace => $"{Esc}[90mTRCE{Esc}[0m",
			LogLevel.Debug => $"{Esc}[36mDBUG{Esc}[0m",
			LogLevel.Information => $"{Esc}[32mINFO{Esc}[0m",
			LogLevel.Warning => $"{Esc}[33mWARN{Esc}[0m",
			LogLevel.Error => $"{Esc}[31mFAIL{Esc}[0m",
			LogLevel.Critical => $"{Esc}[1;31mCRIT{Esc}[0m",
			_ => "????",
		};

	private static string ShortenCategory(string category)
	{
		var s = category;
		if (s.StartsWith("Microsoft.", StringComparison.Ordinal))
		{
			s = s[10..];
		}
		else if (s.StartsWith("System.", StringComparison.Ordinal))
		{
			s = s[7..];
		}
		while (s.Length > CategoryWidth)
		{
			var dot = s.IndexOf('.');
			if (dot < 0 || dot >= s.Length - 1)
			{
				break;
			}
			s = s[(dot + 1)..];
		}
		return s.Length <= CategoryWidth ? s.PadRight(CategoryWidth) : "…" + s[^(CategoryWidth - 1)..];
	}

	private static string Compact(string message)
	{
		const int max = 240;
		var oneLine = message.IndexOfAny(['\n', '\r']) >= 0 ? message.Replace('\n', ' ').Replace('\r', ' ') : message;
		return oneLine.Length <= max ? oneLine : oneLine[..(max - 1)] + "…";
	}

	private static void WriteTrace(TextWriter writer)
	{
		var activity = Activity.Current;
		if (activity is null)
		{
			return;
		}
		var traceId = activity.TraceId.ToHexString();
		if (traceId.Length == 0)
		{
			return;
		}
		writer.Write(' ');
		writer.Write(Gray);
		writer.Write("trace=");
		writer.Write(traceId.AsSpan(0, Math.Min(8, traceId.Length)));
		writer.Write(Reset);
	}
}
