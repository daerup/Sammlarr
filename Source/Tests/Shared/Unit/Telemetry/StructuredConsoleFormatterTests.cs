using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Telemetry;

namespace Shared.Tests.Unit.Telemetry;

public class StructuredConsoleFormatterTests
{
	[Test]
	public async Task Write_IncludesLevelCategoryAndMessage()
	{
		var output = Format(LogLevel.Information, "Sammlarr.Test", "hello world");

		await Assert.That(output).Contains("INFO");
		await Assert.That(output).Contains("Sammlarr.Test");
		await Assert.That(output).Contains("hello world");
	}

	[Test]
	public async Task Write_StripsMicrosoftPrefixFromCategory()
	{
		var output = Format(LogLevel.Information, "Microsoft.EntityFrameworkCore.Database.Command", "msg");

		await Assert.That(output).DoesNotContain("Microsoft.EntityFrameworkCore");
		await Assert.That(output).Contains("Database.Command");
	}

	[Test]
	public async Task Write_OmitsStructuredAttributes_LeavingOnlyRenderedMessage()
	{
		var state = new[]
		{
			new KeyValuePair<string, object?>("count", 42),
			new KeyValuePair<string, object?>("provider", "Plex"),
			new KeyValuePair<string, object?>("{OriginalFormat}", "Loaded {count} for {provider}"),
		};
		var entry = new LogEntry<IReadOnlyList<KeyValuePair<string, object?>>>(
			LogLevel.Warning,
			"Cat",
			new EventId(0),
			state,
			null,
			(_, _) => "Loaded 42 for Plex"
		);

		var sw = new StringWriter();
		new StructuredConsoleFormatter().Write(entry, null, sw);

		var output = sw.ToString();
		await Assert.That(output).Contains("Loaded 42 for Plex");
		await Assert.That(output).DoesNotContain("count=42");
		await Assert.That(output).DoesNotContain("provider=Plex");
		await Assert.That(output).DoesNotContain("{OriginalFormat}");
	}

	[Test]
	public async Task Write_FlattensNewlinesInMessage()
	{
		var output = Format(LogLevel.Information, "Cat", "line one\nline two\rline three");

		await Assert.That(output).Contains("line one line two line three");
		await Assert.That(output.TrimEnd('\r', '\n')).DoesNotContain("\n");
	}

	[Test]
	public async Task Write_TruncatesLongMessagesWithEllipsis()
	{
		var longMessage = new string('a', 500);

		var output = Format(LogLevel.Information, "Cat", longMessage);

		await Assert.That(output).Contains("…");
		await Assert.That(output.Length).IsLessThan(longMessage.Length);
	}

	[Test]
	public async Task Write_EachRecord_EndsWithNewline()
	{
		var output = Format(LogLevel.Information, "Cat", "msg");

		await Assert.That(output).EndsWith(Environment.NewLine);
	}

	[Test]
	public async Task Write_SkipsRecordsWithNoMessageAndNoException()
	{
		var entry = new LogEntry<string>(LogLevel.Information, "Cat", new EventId(0), "ignored", null, (_, _) => string.Empty);

		var sw = new StringWriter();
		new StructuredConsoleFormatter().Write(entry, null, sw);

		await Assert.That(sw.ToString()).IsEmpty();
	}

	private static string Format(LogLevel level, string category, string message)
	{
		var entry = new LogEntry<string>(level, category, new EventId(0), message, null, (s, _) => s);
		var sw = new StringWriter();
		new StructuredConsoleFormatter().Write(entry, null, sw);
		return sw.ToString();
	}
}
