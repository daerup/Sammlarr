using Domain.Feed.Run;
using Domain.Providers;

namespace Domain.Feed.Trace;

public readonly record struct TraceStep(long Seq, int AtMs, Optional<int> Title, TraceEvent Event);

public readonly record struct TraceCursor(Optional<Guid> Run, long Seq)
{
	public static TraceCursor Start => new(new None(), 0);
}

public union TraceRead(TraceSnapshot, TraceBatch, NoTrace);

public sealed record TraceSnapshot(
	TraceCursor Cursor,
	int ElapsedMs,
	IReadOnlyList<TitleInFlight> Titles,
	IReadOnlyList<NodeTally> Nodes,
	FateTally Fates,
	Optional<int> Requests,
	Optional<FeedRunEnd> Ended
)
{
	public RunProgress Progress { get; init; } = RunProgress.None;
}

public sealed record TraceBatch(TraceCursor Cursor, int ElapsedMs, IReadOnlyList<TraceStep> Steps, FateTally Fates, Optional<int> Requests, Optional<FeedRunEnd> Ended)
{
	public RunProgress Progress { get; init; } = RunProgress.None;
}

public sealed record RunProgress(Optional<SourcePage> Page, int Waiting)
{
	public static RunProgress None { get; } = new(new None(), 0);

	public Optional<ServiceWait> Busiest { get; init; } = new None();

	public Optional<int> Reading { get; init; } = new None();
}

public readonly record struct ServiceWait(RemoteService Service, int Waiting);

public readonly record struct NoTrace;

public sealed record FeedRunEnd(TraceEnding Ending, int ElapsedMs)
{
	public Optional<FeedRunCounts> Counts { get; init; } = new None();
}

public readonly record struct HopPlace(RemoteService Service, TraceStage Stage);

public readonly record struct TitleInFlight(
	int Title,
	string Name,
	Optional<int> Year,
	Optional<HopPlace> At,
	Optional<RemoteService> LastIds,
	Optional<RemoteService> LastDetails
);

public readonly record struct NodeTally(
	HopPlace Node,
	int Answered,
	int AfterRateLimit,
	int GaveUp,
	int Failed,
	int Waiting,
	int MeanMs
);

public readonly record struct FateTally(int Started, int Known, int Earlier, int Matched, int CouldNotLookUp, int NotFound, int ListedWithoutIds)
{
	public static FateTally None => new(0, 0, 0, 0, 0, 0, 0);

	public int Settled => Matched + CouldNotLookUp + NotFound + ListedWithoutIds;
}
