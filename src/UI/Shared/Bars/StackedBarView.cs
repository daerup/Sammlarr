using System.Globalization;

namespace UI.Shared.Bars;

internal readonly record struct BarPaint(Optional<string> Color, BarPattern Pattern)
{
	internal bool Named => Pattern is BarPattern.Solid && Color.IsSome();

	internal bool Unnamed => Pattern is BarPattern.Solid && Color.IsNone();
}

internal static class StackedBarView
{
	private const string Accent = "var(--chart-1)";

	private const double GoldenAngle = 137.508;

	internal static IReadOnlyList<Optional<string>> Colors(IReadOnlyList<BarPaint> parts)
	{
		return [.. parts.Select((part, index) => part.Pattern is BarPattern.Solid ? part.Color.Or(() => Some.Of(Computed(parts, index))) : new None())];
	}

	internal static string Count(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

	internal static int Height(BarSize size)
	{
		return size switch
		{
			BarSize.Small => 6,
			BarSize.Large => 12,
			BarSize.Line => 3,
		};
	}

	internal static double Radius(BarSize size) => Height(size) / 3.0;

	internal static double Fill(long sum, long total) => total == 0 ? 0 : 100.0 * sum / total;

	private static string Computed(IReadOnlyList<BarPaint> parts, int index)
	{
		List<BarPaint> before = [.. parts.Take(index)];
		var anchorAt = before.FindLastIndex(part => part.Named);
		var anchor = anchorAt < 0 ? Accent : before[anchorAt].Color.GetValueOr(Accent);
		var steps = before.Skip(anchorAt + 1).Count(part => part.Unnamed) + (anchorAt < 0 ? 0 : 1);
		var lightness = before.Count(part => part.Unnamed) % 2 == 0 ? "var(--bar-auto-l)" : "var(--bar-auto-l2)";
		return string.Create(CultureInfo.InvariantCulture, $"oklch(from {anchor} {lightness} var(--bar-auto-c) calc(h + {steps * GoldenAngle:0.#}))");
	}
}
