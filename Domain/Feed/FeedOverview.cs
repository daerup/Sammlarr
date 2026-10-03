using Domain.Feed.Run;

namespace Domain.Feed;

public sealed record FeedOverview(Feed Feed, Optional<FeedRun> LastRun, FeedRunHistory History, int OnWantlist)
{
	public static FeedOverview Of(Feed feed, Optional<FeedRunTotals> runs, int onWantlist)
	{
		return new(feed, runs.Map(totals => totals.LastRun), runs.Map(totals => totals.History).GetValueOr(FeedRunHistory.None), onWantlist);
	}
}
