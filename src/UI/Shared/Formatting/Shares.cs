namespace UI.Shared.Formatting;

internal static class Shares
{
	internal static string Of(long part, long total)
	{
		return (total, part * 1000 < total && part > 0) switch
		{
			(0, _) => "0%",
			(_, true) => "<0.1%",
			_ => FormattableString.Invariant($"{part * 100.0 / total:0.0}%"),
		};
	}
}
