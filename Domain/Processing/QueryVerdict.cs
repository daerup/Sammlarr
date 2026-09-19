namespace Domain.Processing;

public enum QueryOutcome
{
	Qualified,
	Culled,
	Truncated,
}

public readonly record struct Enrollment
{
	public required bool InWatchlist { get; init; }
	public required bool InBlacklist { get; init; }
	public required bool Owned { get; init; }
}

public sealed record QueryVerdict<T>(T Item, QueryOutcome Outcome, Enrollment Enrollment, bool ConsumesSlot);
