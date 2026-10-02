namespace Domain.Providers;

public readonly record struct ProviderLoad(Optional<DateTimeOffset> BlockedUntil, double Busy)
{
	public bool IsOpen(DateTimeOffset now) => BlockedUntil is not Some<DateTimeOffset>(var until) || until <= now;
}
