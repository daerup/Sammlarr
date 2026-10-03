using Domain.Media;
using Domain.Processing;

namespace Domain.Feed.Planning;

public readonly record struct RawFeedLoad(IReadOnlyList<MediaLookup> Items, Completeness Completeness, string? SourceFailure = null);

public readonly record struct ClassifiedFeedLoad(
	IReadOnlyList<QueryVerdict<IdentifiedMedia>> Verdicts,
	IReadOnlyList<UnmatchedMedia> Unmatched,
	Completeness Completeness,
	string? SourceFailure = null
);
