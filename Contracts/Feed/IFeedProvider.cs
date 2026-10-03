using Domain.Media;

namespace Contracts.Feed;

public interface IFeedProvider
{
	IAsyncEnumerable<MediaMatchResult> GetFeedItemsAsync(string identifier, CancellationToken cancellationToken = default);
}
