namespace Domain.Usage;

public sealed record QuotaPoint(DateTimeOffset At, long Left);

public sealed record QuotaWait(DateTimeOffset From, DateTimeOffset Until);

public sealed record QuotaDay(IReadOnlyList<QuotaPoint> Points, IReadOnlyList<QuotaWait> Waits, Optional<DateTimeOffset> ReserveAt)
{
	private static readonly TimeSpan Day = TimeSpan.FromDays(1);

	private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

	private static readonly TimeSpan PaceSpan = TimeSpan.FromHours(2);

	public static QuotaDay Of(IReadOnlyList<(DateTimeOffset Hour, long Spent)> spent, QuotaAllowance allowance, DateTimeOffset now)
	{
		var reset = OnTheHour(allowance.ResetAt - Day);
		var marks = Enumerable.Range(0, 24).Select(hour => reset + (Hour * hour)).TakeWhile(at => at < now);
		List<QuotaPoint> points = [.. marks.Select(at => new QuotaPoint(at, Left(at, spent, allowance))), new QuotaPoint(now, allowance.Remaining)];
		return new QuotaDay(points, WaitsOf(points, allowance), ReserveReach(spent, allowance, reset, now));
	}

	private static DateTimeOffset OnTheHour(DateTimeOffset at) => at.AddTicks(-(at.UtcTicks % TimeSpan.TicksPerHour));

	private static long Left(DateTimeOffset at, IReadOnlyList<(DateTimeOffset Hour, long Spent)> spent, QuotaAllowance allowance)
	{
		var left = allowance.Remaining + spent.Where(hour => hour.Hour >= at).Sum(hour => hour.Spent);
		return allowance.Allowance.Match(cap => Math.Min(cap, left), () => left);
	}

	private static IReadOnlyList<QuotaWait> WaitsOf(IReadOnlyList<QuotaPoint> points, QuotaAllowance allowance)
	{
		var from = allowance.ReserveReachedAt.Or(() => points.FirstOrNone(point => point.Left <= allowance.Reserve).Map(point => point.At));
		return [.. new[] { from }.WhereSome().Select(start => new QuotaWait(start, allowance.ResetAt))];
	}

	private static Optional<DateTimeOffset> ReserveReach(
		IReadOnlyList<(DateTimeOffset Hour, long Spent)> spent,
		QuotaAllowance allowance,
		DateTimeOffset reset,
		DateTimeOffset now
	)
	{
		var since = reset + (Hour * Math.Floor((now - reset) / Hour)) - PaceSpan;
		var paced = spent.Where(hour => hour.Hour >= since).Sum(hour => hour.Spent);
		return Optional
			.When(
				allowance.Remaining > allowance.Reserve && paced > 0,
				() => now + (Hour * ((allowance.Remaining - allowance.Reserve) * ((now - since) / Hour) / paced))
			)
			.When(reached => reached < allowance.ResetAt);
	}
}
