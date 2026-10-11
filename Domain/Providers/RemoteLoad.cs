namespace Domain.Providers;

public readonly record struct RemoteLoad(
	Optional<DateTimeOffset> BlockedUntil,
	TimeSpan EstimatedWait,
	bool Down,
	int Waiting,
	TimeSpan MaxWait = default,
	TimeSpan EnrichWait = default
)
{
	public static RemoteLoad Idle { get; } = new(new None(), TimeSpan.Zero, false, 0);

	public bool IsOpen(DateTimeOffset now) => !Down && BlockedUntil.Match(until => until <= now, () => true);

	public bool Takes(DateTimeOffset now, TimeSpan budget) => !Down && BlockedUntil.Match(until => until - now <= budget, () => true);

	public double Weight(int cost) => cost * (1 + EstimatedWait.TotalSeconds);
}
