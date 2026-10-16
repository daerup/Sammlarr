using Domain.Feed;
using Domain.Feed.Trace;

namespace Contracts.Feed;

public interface IFeedRunLayout
{
	Task<RunLayout> GetAsync(FeedId feed, CancellationToken cancellationToken = default);
}
