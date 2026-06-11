namespace Contracts.Watchlist;

public readonly record struct RemoteOrigin;

public readonly record struct ManualOrigin;

public readonly record struct FeedOrigin(Guid FeedId, string FeedName);

public readonly record struct OrphanedOrigin(string FormerFeedName);

public union WatchlistItemProvenance(RemoteOrigin, ManualOrigin, FeedOrigin, OrphanedOrigin);

