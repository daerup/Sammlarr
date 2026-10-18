using System.Globalization;

namespace Domain.Processing;

public sealed record Duration(double Amount, DurationUnit Unit)
{
	public double Minutes
	{
		get
		{
			return Unit switch
			{
				DurationUnit.Minutes => Amount,
				DurationUnit.Hours => Amount * 60,
			};
		}
	}

	public string ToIso() => $"PT{Amount.ToString("0.##########", CultureInfo.InvariantCulture)}{Letter(Unit)}";

	public static Optional<Duration> FromIso(string text)
	{
		return Optional
			.When(text.Length > 3 && text.StartsWith("PT", StringComparison.Ordinal), () => text[^1])
			.Map(UnitOf)
			.Map(unit => AmountOf(text).Map(amount => new Duration(amount, unit)));
	}

	private static Optional<double> AmountOf(string text)
	{
		var parsed = double.TryParse(text.AsSpan(2, text.Length - 3), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount);
		return Optional.When(parsed, () => amount);
	}

	private static char Letter(DurationUnit unit)
	{
		return unit switch
		{
			DurationUnit.Minutes => 'M',
			DurationUnit.Hours => 'H',
		};
	}

	private static Optional<DurationUnit> UnitOf(char letter)
	{
		return letter switch
		{
			'M' => Some.Of(DurationUnit.Minutes),
			'H' => Some.Of(DurationUnit.Hours),
			_ => new None(),
		};
	}
}

public enum DurationUnit
{
	Minutes,
	Hours,
}
