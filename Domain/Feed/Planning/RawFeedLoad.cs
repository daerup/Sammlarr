using Domain.Media;
using Domain.Processing;

namespace Domain.Feed.Planning;

public readonly record struct RawFeedLoad(IReadOnlyList<MediaLookup> Items, FeedLoadState State, string? SourceFailure = null);

public readonly record struct ClassifiedFeedLoad(
	IReadOnlyList<QueryVerdict<IdentifiedMedia>> Verdicts,
	IReadOnlyList<UnmatchedMedia> Unmatched,
	FeedLoadState State,
	string? SourceFailure = null
);
