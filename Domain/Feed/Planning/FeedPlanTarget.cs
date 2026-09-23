namespace Domain.Feed.Planning;

public union FeedPlanTarget(SavedFeed, DraftFeed);

public readonly record struct SavedFeed(FeedId Id);

public readonly record struct DraftFeed(Feed Definition, RawFeedLoad Load);
