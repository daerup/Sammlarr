namespace Domain.Processing;

public enum QueryOutcome
{
	Qualified,
	Culled,
	Truncated,
}

public readonly record struct Enrollment
{
	public required bool InWantlist { get; init; }
	public required bool InBlocklist { get; init; }
	public required bool Owned { get; init; }
}

public sealed record QueryVerdict<T>(T Item, QueryOutcome Outcome, Enrollment Enrollment, bool ConsumesSlot);
