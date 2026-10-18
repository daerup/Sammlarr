using System.Globalization;

namespace Domain.Processing;

public sealed record RelativeDate(int Amount, DateUnit Unit, bool Ahead)
{
	public DateOnly On(DateOnly today)
	{
		var amount = Ahead ? Amount : -Amount;
		return Unit switch
		{
			DateUnit.Days => today.AddDays(amount),
			DateUnit.Weeks => today.AddDays(7 * amount),
			DateUnit.Months => today.AddMonths(amount),
			DateUnit.Years => today.AddYears(amount),
		};
	}

	public string ToIso() => $"{(Ahead ? string.Empty : "-")}P{Amount.ToString(CultureInfo.InvariantCulture)}{Letter(Unit)}";

	public static Optional<RelativeDate> FromIso(string text)
	{
		var ahead = !text.StartsWith('-');
		var body = ahead ? text : text[1..];
		return Optional
			.When(body.Length > 2 && body[0] == 'P', () => body[^1])
			.Map(UnitOf)
			.Map(unit => AmountOf(body).Map(amount => new RelativeDate(amount, unit, ahead)));
	}

	private static Optional<int> AmountOf(string body)
	{
		return Optional.When(int.TryParse(body.AsSpan(1, body.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out var amount), () => amount);
	}

	private static char Letter(DateUnit unit)
	{
		return unit switch
		{
			DateUnit.Days => 'D',
			DateUnit.Weeks => 'W',
			DateUnit.Months => 'M',
			DateUnit.Years => 'Y',
		};
	}

	private static Optional<DateUnit> UnitOf(char letter)
	{
		return letter switch
		{
			'D' => Some.Of(DateUnit.Days),
			'W' => Some.Of(DateUnit.Weeks),
			'M' => Some.Of(DateUnit.Months),
			'Y' => Some.Of(DateUnit.Years),
			_ => new None(),
		};
	}
}

public enum DateUnit
{
	Days,
	Weeks,
	Months,
	Years,
}
