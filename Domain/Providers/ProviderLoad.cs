namespace Domain.Providers;

public readonly record struct ProviderLoad(Optional<DateTimeOffset> BlockedUntil, double Busy, TimeSpan MaxWait = default, TimeSpan EnrichWait = default)
{
	public bool IsOpen(DateTimeOffset now) => BlockedUntil.Match(until => until <= now, () => true);

	public bool FreesWithin(DateTimeOffset now, TimeSpan wait) => BlockedUntil.Match(until => until - now <= wait, () => true);

	public DateTimeOffset FreeAt(DateTimeOffset now) => BlockedUntil.GetValueOr(now);
}
