using Domain.Media;
using Domain.Processing;

namespace Domain.Feed.Planning;

public readonly record struct RawFeedLoad(IReadOnlyList<MediaLookup> Items, Completeness Completeness);

public readonly record struct ClassifiedFeedLoad(
	IReadOnlyList<QueryVerdict<MediaLookup>> Verdicts,
	IReadOnlyList<UnmatchedMedia> Unresolved,
	Completeness Completeness
);
