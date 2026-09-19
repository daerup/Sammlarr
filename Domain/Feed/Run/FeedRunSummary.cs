using Domain.Media;
using Domain.Processing;

namespace Domain.Feed.Run;

public sealed record FeedRunSummary(
	IReadOnlyList<QueryVerdict<MediaLookup>> Verdicts,
	IReadOnlyList<MediaLookup> Unresolved,
	FeedDelta Delta,
	Conclusiveness Conclusiveness
);
