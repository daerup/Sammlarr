using Domain.Providers;

namespace Domain.Watchlist;

public readonly record struct RemoteOrigin(WatchlistProvider Provider);

public readonly record struct ManualOrigin(ManualSource Source);

public readonly record struct FeedOrigin(Guid FeedId, string FeedName, FeedProvider Provider);

public readonly record struct OrphanedOrigin(string FormerFeedName);

public union WatchlistItemProvenance(RemoteOrigin, ManualOrigin, FeedOrigin, OrphanedOrigin);

