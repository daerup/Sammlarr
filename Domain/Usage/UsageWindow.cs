namespace Domain.Usage;

public enum UsageRange
{
	Day,
	Week,
	Month,
}

public sealed record UsageWindow(DateTimeOffset From, DateTimeOffset To, TimeSpan Step)
{
	public int Slots => (int)((To - From) / Step);

	public static UsageWindow Of(UsageRange range, DateTimeOffset now)
	{
		return range switch
		{
			UsageRange.Day => Hours(now),
			UsageRange.Week => Days(now, 7),
			UsageRange.Month => Days(now, 30),
		};
	}

	public UsageWindow Before() => this with { From = From - (To - From), To = From };

	public DateTimeOffset SlotAt(int index) => From + (Step * index);

	public bool Contains(DateTimeOffset at) => at >= From && at < To;

	public int SlotOf(DateTimeOffset at) => (int)((at - From) / Step);

	private static UsageWindow Hours(DateTimeOffset now)
	{
		var next = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Offset).AddHours(1);
		return new UsageWindow(next.AddHours(-24), next, TimeSpan.FromHours(1));
	}

	private static UsageWindow Days(DateTimeOffset now, int days)
	{
		var tomorrow = new DateTimeOffset(now.Date, now.Offset).AddDays(1);
		return new UsageWindow(tomorrow.AddDays(-days), tomorrow, TimeSpan.FromDays(1));
	}
}
