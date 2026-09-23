using Domain.Media;

namespace Domain.Feed.Run;

public readonly record struct FeedLoad(IReadOnlyList<MediaLookup> Items, Completeness Completeness);
