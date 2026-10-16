namespace Core.Usage;

internal static class Medians
{
	public static double Of(IEnumerable<double> values)
	{
		var sorted = values.Order().ToList();
		var middle = sorted.Count / 2;
		return sorted.Count switch
		{
			0 => 0,
			_ when sorted.Count % 2 == 1 => sorted[middle],
			_ => (sorted[middle - 1] + sorted[middle]) / 2,
		};
	}
}
