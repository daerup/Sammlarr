namespace Domain.Usage;

public sealed record QuotaPoint(DateTimeOffset At, long Left);

public sealed record QuotaWait(DateTimeOffset From, DateTimeOffset Until);

public sealed record QuotaDay(IReadOnlyList<QuotaPoint> Points, IReadOnlyList<QuotaWait> Waits, Optional<DateTimeOffset> ReserveAt)
{
	private static readonly TimeSpan Day = TimeSpan.FromDays(1);

	private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

	private static readonly TimeSpan PaceSpan = TimeSpan.FromHours(2);

	private static readonly TimeSpan Drift = TimeSpan.FromMinutes(1);

	public static QuotaDay Of(IReadOnlyList<(DateTimeOffset Hour, long Spent)> spent, QuotaAllowance allowance, DateTimeOffset now)
	{
		var reset = OnTheHour(allowance.ResetAt - Day);
		var marks = Enumerable.Range(0, 24).Select(hour => reset + Hour * hour).TakeWhile(at => at < now);
		List<QuotaPoint> today = [.. marks.Select(at => new QuotaPoint(at, Left(at, spent, allowance))), new QuotaPoint(now, allowance.Remaining)];
		var before = allowance.DayBefore.When(ended => (ended.ResetAt - (allowance.ResetAt - Day)).Duration() < Drift);
		List<QuotaPoint> ended = [.. Before(before, spent, allowance, reset, now)];
		var waitedBefore = before.Map(day => Wait(ended, day.ReserveReachedAt, allowance.Reserve, reset));
		return new QuotaDay(
			[.. ended, .. today],
			[.. new[] { waitedBefore, Wait(today, allowance.ReserveReachedAt, allowance.Reserve, allowance.ResetAt) }.WhereSome()],
			ReserveReach(spent, allowance, reset, now)
		);
	}

	private static IEnumerable<QuotaPoint> Before(
		Optional<DayEnd> before,
		IReadOnlyList<(DateTimeOffset Hour, long Spent)> spent,
		QuotaAllowance allowance,
		DateTimeOffset reset,
		DateTimeOffset now
	)
	{
		return before.Match(
			ended =>
				Enumerable
					.Range(0, 24)
					.Select(hours => reset - Hour * hours)
					.TakeWhile(at => at > now - Day)
					.Reverse()
					.Select(at => new QuotaPoint(at, Capped(ended.Left + spent.Where(hour => hour.Hour >= at && hour.Hour < reset).Sum(hour => hour.Spent), allowance))),
			Enumerable.Empty<QuotaPoint>
		);
	}

	private static Optional<QuotaWait> Wait(IReadOnlyList<QuotaPoint> points, Optional<DateTimeOffset> reachedAt, long reserve, DateTimeOffset until)
	{
		return reachedAt
			.Or(() => points.FirstOrNone(point => point.Left <= reserve).Map(point => point.At))
			.When(from => from < until)
			.Map(from => new QuotaWait(from, until));
	}

	private static long Capped(long left, QuotaAllowance allowance) => allowance.Allowance.Match(cap => Math.Min(cap, left), () => left);

	private static DateTimeOffset OnTheHour(DateTimeOffset at) => at.AddTicks(-(at.UtcTicks % TimeSpan.TicksPerHour));

	private static long Left(DateTimeOffset at, IReadOnlyList<(DateTimeOffset Hour, long Spent)> spent, QuotaAllowance allowance)
	{
		return Capped(allowance.Remaining + spent.Where(hour => hour.Hour >= at).Sum(hour => hour.Spent), allowance);
	}

	private static Optional<DateTimeOffset> ReserveReach(
		IReadOnlyList<(DateTimeOffset Hour, long Spent)> spent,
		QuotaAllowance allowance,
		DateTimeOffset reset,
		DateTimeOffset now
	)
	{
		var since = reset + Hour * Math.Floor((now - reset) / Hour) - PaceSpan;
		var paced = spent.Where(hour => hour.Hour >= since).Sum(hour => hour.Spent);
		return Optional
			.When(
				allowance.Remaining > allowance.Reserve && paced > 0,
				() => now + Hour * ((allowance.Remaining - allowance.Reserve) * ((now - since) / Hour) / paced)
			)
			.When(reached => reached < allowance.ResetAt);
	}
}
