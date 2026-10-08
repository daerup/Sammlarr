using Domain.Feed;
using Domain.Feed.Run;
using Domain.Providers;
using Domain.Usage;

namespace Contracts.Usage;

public interface IFeedRunUsage
{
	void Start(FeedId feed);

	void Credit(FeedId feed, RemoteService service, UsageOutcome outcome, long calls);

	IReadOnlyList<FeedRunCalls> End(FeedId feed);
}
