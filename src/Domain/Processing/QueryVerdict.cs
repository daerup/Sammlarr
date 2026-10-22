namespace Domain.Processing;

public readonly record struct Enrollment
{
	public required bool InWantlist { get; init; }
	public required bool InBlocklist { get; init; }
	public required bool Owned { get; init; }
}

public sealed record QueryVerdict<T>(T Item, Enrollment Enrollment, QueryOutcome Outcome)
{
	public QueryVerdict<TOut> Map<TOut>(Func<T, TOut> map) => new(map(Item), Enrollment, Outcome);
}
