namespace Domain.Providers;

public readonly record struct ProviderLoad(Optional<DateTimeOffset> BlockedUntil, double Busy, TimeSpan MaxWait = default)
{
	public bool IsOpen(DateTimeOffset now) => BlockedUntil.Match(until => until <= now, () => true);
}
