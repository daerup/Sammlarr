namespace Domain.Usage;

public static class DurationBuckets
{
	public const int Over = int.MaxValue;

	public static readonly IReadOnlyList<int> UpToMs = [5, 10, 25, 50, 75, 100, 250, 500, 750, 1000, 2500, 5000, 7500, 10000, Over];

	public static Optional<double> Percentile(IReadOnlyDictionary<int, long> counts, double q) => Locate(counts, q).Map(spot => Interpolated(spot));

	public static Optional<int> BucketOf(IReadOnlyDictionary<int, long> counts, double q) => Locate(counts, q).Map(spot => UpToMs[spot.Index]);

	private static Optional<Spot> Locate(IReadOnlyDictionary<int, long> counts, double q)
	{
		var total = counts.Values.Sum();
		var rank = q * total;
		var seen = 0L;
		for (var index = 0; index < UpToMs.Count; index++)
		{
			var count = counts.GetValueOrDefault(UpToMs[index]);
			if (total > 0 && count > 0 && seen + count >= rank)
			{
				return Some.Of(new Spot(index, (rank - seen) / count));
			}

			seen += count;
		}

		return new None();
	}

	private static double Interpolated(Spot spot)
	{
		var lower = spot.Index == 0 ? 0 : UpToMs[spot.Index - 1];
		var upper = UpToMs[spot.Index] == Over ? lower : UpToMs[spot.Index];
		return lower + ((upper - lower) * spot.Within);
	}

	private readonly record struct Spot(int Index, double Within);
}
