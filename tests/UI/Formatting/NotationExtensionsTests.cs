using System.Globalization;
using Humanizer;
using UI.Shared.Formatting;

namespace UI.Tests.Formatting;

public class NotationExtensionsTests
{
	[Test]
	[Arguments(0, 2, 15, 0, 0, "2 h 15 min")]
	[Arguments(0, 0, 0, 1, 234, "1 s 234 ms")]
	[Arguments(0, 0, 0, 0, 482, "482 ms")]
	[Arguments(3, 4, 0, 0, 0, "3 d 4 h")]
	public async Task Humanize_InSymbols_WritesTheLargestUnitsUpToThePrecision(int days, int hours, int minutes, int seconds, int milliseconds, string expected)
	{
		var span = new TimeSpan(days, hours, minutes, seconds, milliseconds);

		await Assert.That(span.Humanize(2, Notation.Symbols)).IsEqualTo(expected);
	}

	[Test]
	public async Task Humanize_InSymbols_LeavesOutAnEmptyUnitWithoutCountingIt()
	{
		await Assert.That(new TimeSpan(1, 0, 5).Humanize(2, Notation.Symbols)).IsEqualTo("1 h 5 s");
	}

	[Test]
	public async Task Humanize_InSymbols_StopsAtTheSmallestUnitAsked()
	{
		await Assert.That(new TimeSpan(2, 15, 30).Humanize(3, Notation.Symbols, TimeUnit.Minute)).IsEqualTo("2 h 15 min");
		await Assert.That(TimeSpan.FromSeconds(20).Humanize(2, Notation.Symbols, TimeUnit.Minute)).IsEqualTo("0 min");
	}

	[Test]
	public async Task Humanize_InWords_CountsDaysRatherThanWeeks()
	{
		await Assert.That(new TimeSpan(2, 15, 0).Humanize(2, Notation.Words)).IsEqualTo("2 hours, 15 minutes");
		await Assert.That(TimeSpan.FromDays(365).Humanize(1, Notation.Words)).IsEqualTo("365 days");
	}

	[Test]
	public async Task Humanize_InAGermanBrowser_StaysEnglish()
	{
		CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
		var now = new DateTimeOffset(2026, 9, 27, 14, 0, 0, TimeSpan.Zero);

		await Assert.That(now.AddMinutes(-16).Humanize(now)).IsEqualTo("16 minutes ago");
		await Assert.That(new TimeSpan(2, 15, 0).Humanize(2, Notation.Words)).IsEqualTo("2 hours, 15 minutes");
		await Assert.That(new TimeSpan(2, 15, 0).Humanize(2, Notation.Symbols)).IsEqualTo("2 h 15 min");
	}

	[Test]
	public async Task Humanize_ASizeInSymbols_InAnyCulture_ReadsInTheLargestWholeUnit()
	{
		CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

		await Assert.That(50_920L.Bytes().Humanize("0.#", Notation.Symbols)).IsEqualTo("49.7 KB");
	}

	[Test]
	public async Task Humanize_ASizeInWords_SpellsTheUnitOut()
	{
		await Assert.That(50_920L.Bytes().Humanize("0.#", Notation.Words)).IsEqualTo("49.7 kilobytes");
	}

	[Test]
	public async Task Humanize_AnEmptySize_CountsBytesNotBits()
	{
		await Assert.That(0L.Bytes().Humanize("0.#", Notation.Symbols)).IsEqualTo("0 B");
		await Assert.That(0L.Bytes().Humanize("0.#", Notation.Words)).IsEqualTo("0 bytes");
	}

	[Test]
	[Arguments("de-DE")]
	[Arguments("")]
	public async Task Humanize_AList_InAnyCulture_JoinsInEnglish(string culture)
	{
		CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
		string[] names = ["TMDB", "IMDb", "TVDB"];

		await Assert.That(names.Humanize(Notation.Words)).IsEqualTo("TMDB, IMDb, and TVDB");
		await Assert.That(names.Humanize(Notation.Words, "or")).IsEqualTo("TMDB, IMDb, or TVDB");
		await Assert.That(names.Humanize(Notation.Symbols)).IsEqualTo("TMDB, IMDb, & TVDB");
		await Assert.That(names[..2].Humanize(Notation.Words)).IsEqualTo("TMDB and IMDb");
	}
}
