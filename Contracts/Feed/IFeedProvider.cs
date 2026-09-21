using Domain.Media;

namespace Contracts.Feed;

public interface IFeedProvider
{
	IAsyncEnumerable<MediaLookup> GetFeedItemsAsync(string identifier, CancellationToken cancellationToken = default);
}
