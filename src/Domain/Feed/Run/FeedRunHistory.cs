namespace Domain.Feed.Run;

public readonly record struct FeedRunHistory
{
	public required int TotalRuns { get; init; }
	public required Optional<DateTimeOffset> FirstRunAt { get; init; }
	public required int TotalAdded { get; init; }
	public required int TotalRemoved { get; init; }

	public static readonly FeedRunHistory None = new()
	{
		TotalRuns = 0,
		FirstRunAt = new None(),
		TotalAdded = 0,
		TotalRemoved = 0,
	};
}
