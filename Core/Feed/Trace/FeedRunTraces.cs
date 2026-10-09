using Contracts.Feed;
using Contracts.Identity;
using Domain.Feed;
using Domain.Feed.Trace;

namespace Core.Feed.Trace;

internal sealed class FeedRunTraces(FeedRunTrace trace, ICurrentUser user) : IFeedRunTraces
{
	public Task<TraceRead> ReadAsync(FeedId feed, TraceCursor after, CancellationToken cancellationToken = default) =>
		Task.FromResult(trace.Read(feed, user.Id, after));
}
