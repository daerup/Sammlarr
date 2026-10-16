using Domain.Feed;
using Domain.Feed.Run;

namespace Contracts.Feed;

public interface IFeedRunEngine
{
	Task<FeedRunResult> RunAsync(FeedId feedId, FeedRunTrigger trigger, CancellationToken cancellationToken = default);
}
