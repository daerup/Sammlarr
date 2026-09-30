using Domain.Feed.Run;
using Domain.Providers;

namespace Domain.Wantlist;

public readonly record struct SinkOrigin(SinkKind Provider);

public readonly record struct ManualOrigin(ManualSource Source);

public enum ManualSource { Unknown, Search, Browse, FeedPreview }

public readonly record struct FeedOrigin(Feed.Feed Feed, FeedRun Run);

public readonly record struct OrphanedOrigin(string FormerFeedName);

public union WantlistItemProvenance(SinkOrigin, ManualOrigin, FeedOrigin, OrphanedOrigin);
