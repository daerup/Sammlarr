namespace Domain.Feed.Run;

public sealed record FeedRun
{
	public const string Interrupted = "Sammlarr stopped while this run was going.";

	public const string Cancelled = "The run was cancelled.";

	public required RunId Id { get; init; }
	public required FeedId FeedId { get; init; }
	public required DateTimeOffset StartedAt { get; init; }
	public required TimeSpan Duration { get; init; }
	public required FeedStrategy Strategy { get; init; }
	public required FeedRunTrigger Trigger { get; init; }
	public required Conclusiveness Conclusiveness { get; init; }
	public required FeedRunCounts Counts { get; init; }
	public required IReadOnlyList<FeedRunItem> Items { get; init; }

	public Optional<string> SourceFailure { get; init; } = new None();
	public IReadOnlyList<FeedRunCalls> Calls { get; init; } = [];
	public IReadOnlyList<FeedRunTimes> Times { get; init; } = [];

	public bool HasChanges => Counts.Added + Counts.Removed > 0;

	public bool Failed => Counts.Total == 0 && Conclusiveness is not (Conclusiveness.Conclusive or Conclusiveness.Pending);
}
