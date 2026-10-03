using Domain.Media;
using Domain.Processing;

namespace Domain.Feed.Planning;

public readonly record struct RawFeedLoad(IReadOnlyList<MediaMatchResult> Items, FeedLoadState State, string? SourceFailure = null);

public readonly record struct ClassifiedFeedLoad(
	IReadOnlyList<QueryVerdict<MatchedMedia>> Verdicts,
	IReadOnlyList<UnmatchedMedia> Unmatched,
	FeedLoadState State,
	string? SourceFailure = null
);
