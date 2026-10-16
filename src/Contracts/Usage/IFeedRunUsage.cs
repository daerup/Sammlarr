using Domain.Feed;
using Domain.Feed.Run;
using Domain.Providers;
using Domain.Usage;

namespace Contracts.Usage;

public sealed record FeedRunCost(IReadOnlyList<FeedRunCalls> Calls, IReadOnlyList<FeedRunTimes> Times)
{
	public static readonly FeedRunCost None = new([], []);
}

public interface IFeedRunUsage
{
	void Start(FeedId feed);

	void Credit(FeedId feed, RemoteService service, UsageOutcome outcome, long calls);

	void Time(FeedId feed, RemoteService service, int upToMs);

	FeedRunCost End(FeedId feed);
}
