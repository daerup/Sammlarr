namespace Domain.Usage;

public sealed record QuotaDayUse(DateTimeOffset Start, IReadOnlyDictionary<UsageCause, long> Spent)
{
	public long Total => Spent.Values.Sum();
}

public static class QuotaDays
{
	private static readonly TimeSpan Day = TimeSpan.FromDays(1);

	public static IReadOnlyList<QuotaDayUse> Of(IEnumerable<(DateTimeOffset Hour, UsageCause Cause, long Spent)> rows, DateTimeOffset nextReset, int days)
	{
		var first = nextReset - (Day * days);
		var byDay = rows.Where(row => row.Hour >= first && row.Hour < nextReset).ToLookup(row => (int)Math.Floor((row.Hour - first) / Day));
		return
		[
			.. Enumerable
				.Range(0, days)
				.Select(day => new QuotaDayUse(
					first + (Day * day),
					byDay[day].GroupBy(row => row.Cause).ToDictionary(cause => cause.Key, cause => cause.Sum(row => row.Spent))
				)),
		];
	}
}
