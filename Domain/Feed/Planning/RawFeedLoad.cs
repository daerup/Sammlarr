using Domain.Media;
using Domain.Processing;

namespace Domain.Feed.Planning;

public readonly record struct RawFeedLoad(IReadOnlyList<MediaMatchResult> Items, FeedLoadState State, Optional<string> SourceFailure)
{
	public RawFeedLoad(IReadOnlyList<MediaMatchResult> items, FeedLoadState state)
		: this(items, state, new None()) { }
}

public readonly record struct ClassifiedFeedLoad(
	IReadOnlyList<QueryVerdict<MatchedMedia>> Verdicts,
	IReadOnlyList<UnmatchedMedia> Unmatched,
	FeedLoadState State,
	Optional<string> SourceFailure
)
{
	public ClassifiedFeedLoad(IReadOnlyList<QueryVerdict<MatchedMedia>> verdicts, IReadOnlyList<UnmatchedMedia> unmatched, FeedLoadState state)
		: this(verdicts, unmatched, state, new None()) { }
}
