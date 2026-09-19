using Domain.Feed.Run;
using Domain.Media;

namespace Contracts.Feed;

public interface IFeedRunEngine
{
	Task<FeedRunSummary> DryRunAsync(Domain.Feed.Feed feed, IReadOnlyList<MediaLookup> items, FeedSource source, CancellationToken cancellationToken = default);
}
