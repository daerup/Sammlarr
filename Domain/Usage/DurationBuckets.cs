namespace Domain.Usage;

public static class DurationBuckets
{
	public const int Over = int.MaxValue;

	public static readonly IReadOnlyList<int> UpToMs = [5, 10, 25, 50, 75, 100, 250, 500, 750, 1000, 2500, 5000, 7500, 10000, Over];

	public static Optional<double> Percentile(IReadOnlyDictionary<int, long> counts, double q)
	{
		var total = counts.Values.Sum();
		var rank = q * total;
		var seen = 0L;
		for (var index = 0; index < UpToMs.Count; index++)
		{
			var count = counts.GetValueOrDefault(UpToMs[index]);
			if (total > 0 && count > 0 && seen + count >= rank)
			{
				var lower = index == 0 ? 0 : UpToMs[index - 1];
				var upper = UpToMs[index] == Over ? lower : UpToMs[index];
				return Some.Of(lower + ((upper - lower) * ((rank - seen) / count)));
			}

			seen += count;
		}

		return new None();
	}
}
