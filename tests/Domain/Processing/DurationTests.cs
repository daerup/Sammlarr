using CsCheck;
using Domain.Processing;
using TestInfrastructure;

namespace Domain.Tests.Processing;

public class DurationTests
{
	private static readonly Gen<Duration> Durations = Gen.Select(
		Gen.Int[0, 20000],
		Gen.Enum<DurationUnit>(),
		(hundredths, unit) => new Duration(hundredths / 100.0, unit)
	);

	[Test]
	[Arguments(90.0, DurationUnit.Minutes, 90.0)]
	[Arguments(2.0, DurationUnit.Hours, 120.0)]
	[Arguments(1.5, DurationUnit.Hours, 90.0)]
	public async Task Minutes_ForAnAmountAndUnit_CountsMinutes(double amount, DurationUnit unit, double expected)
	{
		await Assert.That(new Duration(amount, unit).Minutes).IsEqualTo(expected);
	}

	[Test]
	[Arguments(2.0, DurationUnit.Hours, "PT2H")]
	[Arguments(1.5, DurationUnit.Hours, "PT1.5H")]
	[Arguments(30.0, DurationUnit.Minutes, "PT30M")]
	public async Task ToIso_ForADuration_WritesIt(double amount, DurationUnit unit, string expected)
	{
		await Assert.That(new Duration(amount, unit).ToIso()).IsEqualTo(expected);
	}

	[Test]
	[Arguments("120")]
	[Arguments("PTH")]
	[Arguments("PT2D")]
	[Arguments("-P2Y")]
	public async Task FromIso_ForTextThatIsNoDuration_FindsNothing(string text)
	{
		await Assert.That(Duration.FromIso(text)).IsNone();
	}

	[Test]
	public void FromIso_ForAnyWrittenDuration_ReadsItBack()
	{
		Durations.Sample(duration => Duration.FromIso(duration.ToIso()).Map(read => read == duration).GetValueOr(false));
	}
}
