using Domain.Feed;
using Domain.Feed.Run;
using Domain.Identity;

namespace Contracts.Feed;

public interface IFeedRunTrace
{
	IDisposable Start(FeedId feed, UserId owner);

	void Counted(FeedId feed, FeedRunCounts counts);

	void End(FeedId feed, FeedRunResult result);
}
