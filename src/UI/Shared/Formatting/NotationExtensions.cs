using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Humanizer;

namespace UI.Shared.Formatting;

internal enum Notation
{
	Words,
	Symbols,
}

internal static class NotationExtensions
{
	private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");

	private static readonly ICollectionFormatter EnglishLists = Configurator.CollectionFormatters.ResolveForCulture(English);

	private static readonly (TimeUnit Unit, Func<TimeSpan, int> Count)[] Units =
	[
		(TimeUnit.Day, span => (int)span.TotalDays),
		(TimeUnit.Hour, span => span.Hours),
		(TimeUnit.Minute, span => span.Minutes),
		(TimeUnit.Second, span => span.Seconds),
		(TimeUnit.Millisecond, span => span.Milliseconds),
	];

	extension(TimeSpan span)
	{
		[SuppressMessage("ApiDesign", "RS0030", Justification = "The one place that asks Humanizer for its own TimeSpan wording.")]
		internal string Humanize(int precision, Notation notation, TimeUnit minUnit = TimeUnit.Millisecond)
		{
			return notation switch
			{
				Notation.Words => span.Humanize(precision, English, TimeUnit.Day, minUnit),
				Notation.Symbols => Symbols(span, precision, minUnit),
			};
		}
	}

	extension(ByteSize size)
	{
		[SuppressMessage("ApiDesign", "RS0030", Justification = "The one place that asks Humanizer for its own size wording.")]
		internal string Humanize(string format, Notation notation)
		{
			return (notation, size.Bytes == 0) switch
			{
				(Notation.Words, true) => "byte".ToQuantity(0),
				(Notation.Symbols, true) => size.ToString($"{format} B", CultureInfo.InvariantCulture),
				(Notation.Words, false) => size.ToFullWords(format, CultureInfo.InvariantCulture),
				(Notation.Symbols, false) => size.ToString(format, CultureInfo.InvariantCulture),
			};
		}
	}

	extension(DateTimeOffset at)
	{
		[SuppressMessage("ApiDesign", "RS0030", Justification = "The one place that asks Humanizer for its own relative time wording.")]
		internal string Humanize(DateTimeOffset now) => at.Humanize(now, English);
	}

	extension<T>(IEnumerable<T> items)
	{
		internal string Humanize(Notation notation, string conjunction = "and")
		{
			return notation switch
			{
				Notation.Words => EnglishLists.Humanize(items, conjunction),
				Notation.Symbols => EnglishLists.Humanize(items, "&"),
			};
		}
	}

	private static string Symbols(TimeSpan span, int precision, TimeUnit minUnit)
	{
		var parts = Units
			.Where(unit => unit.Unit >= minUnit)
			.Select(unit => (unit.Unit, Count: unit.Count(span.Duration())))
			.Where(part => part.Count > 0)
			.Take(precision)
			.Select(part => $"{part.Count} {part.Unit.ToSymbol(English)}");
		return string.Join(" ", parts.DefaultIfEmpty($"0 {minUnit.ToSymbol(English)}"));
	}
}
