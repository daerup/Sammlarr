namespace Domain.Usage;

public sealed record UsageTally(IReadOnlyDictionary<UsageOutcome, long> Calls, IReadOnlyDictionary<int, long> Durations)
{
	public static readonly UsageTally Empty = new(new Dictionary<UsageOutcome, long>(), new Dictionary<int, long>());

	public long Total => Calls.Values.Sum();

	public long Errors => Of(UsageOutcome.Failed) + Of(UsageOutcome.GaveUpOn429);

	public long RateLimited => Of(UsageOutcome.AnsweredAfter429) + Of(UsageOutcome.GaveUpOn429);

	public long Answered => Of(UsageOutcome.Answered) + Of(UsageOutcome.AnsweredAfter429);

	public long Of(UsageOutcome outcome) => Calls.GetValueOrDefault(outcome);

	public Optional<double> Percentile(double q) => DurationBuckets.Percentile(Durations, q);

	public static UsageTally Sum(IEnumerable<UsageTally> tallies)
	{
		var all = tallies.ToList();
		return new UsageTally(Summed(all.Select(tally => tally.Calls)), Summed(all.Select(tally => tally.Durations)));
	}

	private static Dictionary<TKey, long> Summed<TKey>(IEnumerable<IReadOnlyDictionary<TKey, long>> counts)
		where TKey : notnull
	{
		return counts.SelectMany(each => each).GroupBy(pair => pair.Key).ToDictionary(group => group.Key, group => group.Sum(pair => pair.Value));
	}
}
