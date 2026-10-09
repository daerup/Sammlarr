using Domain.Feed;
using Domain.Feed.Trace;

namespace Contracts.Feed;

public interface IFeedRunTraces
{
	Task<TraceRead> ReadAsync(FeedId feed, TraceCursor after, CancellationToken cancellationToken = default);
}
