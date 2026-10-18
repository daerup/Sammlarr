using System.Globalization;
using CsCheck;
using Domain.Processing;
using TestInfrastructure;

namespace Domain.Tests.Processing;

public class RelativeDateTests
{
	private static readonly DateOnly Today = new(2026, 10, 18);

	private static readonly Gen<RelativeDate> Dates = Gen.Select(
		Gen.Int[0, 500],
		Gen.Enum<DateUnit>(),
		Gen.Bool,
		(amount, unit, ahead) => new RelativeDate(amount, unit, ahead)
	);

	[Test]
	[Arguments(2, DateUnit.Years, false, "2024-10-18")]
	[Arguments(3, DateUnit.Weeks, false, "2026-09-27")]
	[Arguments(6, DateUnit.Months, false, "2026-04-18")]
	[Arguments(30, DateUnit.Days, true, "2026-11-17")]
	[Arguments(0, DateUnit.Days, false, "2026-10-18")]
	public async Task On_ForAnAmountAndUnit_CountsFromToday(int amount, DateUnit unit, bool ahead, string expected)
	{
		var day = new RelativeDate(amount, unit, ahead).On(Today);

		await Assert.That(day).IsEqualTo(DateOnly.Parse(expected, CultureInfo.InvariantCulture));
	}

	[Test]
	[Arguments(2, DateUnit.Years, false, "-P2Y")]
	[Arguments(30, DateUnit.Days, true, "P30D")]
	public async Task ToIso_ForADate_WritesASignedDuration(int amount, DateUnit unit, bool ahead, string expected)
	{
		await Assert.That(new RelativeDate(amount, unit, ahead).ToIso()).IsEqualTo(expected);
	}

	[Test]
	[Arguments("2016-10-18T00:00:00Z")]
	[Arguments("P")]
	[Arguments("-P2X")]
	[Arguments("P-2Y")]
	[Arguments("")]
	[Arguments("PT5M")]
	[Arguments("P1Y2M")]
	public async Task FromIso_ForTextThatIsNoDuration_FindsNothing(string text)
	{
		await Assert.That(RelativeDate.FromIso(text)).IsNone();
	}

	[Test]
	public void FromIso_ForAnyWrittenDate_ReadsItBack()
	{
		Dates.Sample(date => RelativeDate.FromIso(date.ToIso()).Map(read => read == date).GetValueOr(false));
	}
}
