using System.Globalization;

namespace API.Tests;

public class ProviderValuesTests
{
	[Test]
	[Arguments(null)]
	[Arguments("")]
	[Arguments(" \t")]
	public async Task Text_WhenBlank_IsNull(string? value)
	{
		await Assert.That(ProviderValues.Text(value)).IsNull();
	}

	[Test]
	public async Task Date_UnderACultureWithAnotherCalendar_ParsesTheIsoDateInvariantly()
	{
		CultureInfo.CurrentCulture = new CultureInfo("th-TH");

		await Assert.That(ProviderValues.Date("2010-07-16")).IsEqualTo(new DateOnly(2010, 7, 16));
	}

	[Test]
	[Arguments(null)]
	[Arguments("")]
	[Arguments("not a date")]
	public async Task Date_WhenItIsNoDate_IsNull(string? value)
	{
		await Assert.That(ProviderValues.Date(value)).IsNull();
	}

	[Test]
	public async Task CountsAndDurations_WhenZero_AreNull()
	{
		using var _ = Assert.Multiple();
		await Assert.That(ProviderValues.Count(0)).IsNull();
		await Assert.That(ProviderValues.Minutes(0)).IsNull();
		await Assert.That(ProviderValues.Milliseconds(0)).IsNull();
		await Assert.That(ProviderValues.YearStart(0)).IsNull();
	}

	[Test]
	public async Task CountsAndDurations_WhenPositive_KeepTheirValue()
	{
		using var _ = Assert.Multiple();
		await Assert.That(ProviderValues.Count(5)).IsEqualTo(5);
		await Assert.That(ProviderValues.Minutes(90)).IsEqualTo(TimeSpan.FromMinutes(90));
		await Assert.That(ProviderValues.Milliseconds(60_000)).IsEqualTo(TimeSpan.FromMinutes(1));
		await Assert.That(ProviderValues.YearStart(1999)).IsEqualTo(new DateOnly(1999, 1, 1));
	}

	[Test]
	public async Task Genres_WithBlanksAndCaseDuplicates_KeepsTheFirstOfEach()
	{
		await Assert.That(ProviderValues.Genres(["Drama", " ", null, "drama", "Thriller"])).IsEquivalentTo(["Drama", "Thriller"]);
	}
}
