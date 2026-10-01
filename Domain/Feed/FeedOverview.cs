using Domain.Feed.Run;

namespace Domain.Feed;

public sealed record FeedOverview(Feed Feed, Optional<FeedRun> LastRun, FeedRunHistory History, int OnWantlist)
{
	public static FeedOverview Of(Feed feed, FeedRunTotals? runs, int onWantlist) =>
		runs is null ? new(feed, new None(), FeedRunHistory.None, onWantlist) : new(feed, Some.Of(runs.LastRun), runs.History, onWantlist);
}
