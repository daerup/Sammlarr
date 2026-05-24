using Contracts.Media.Search;

namespace Contracts.Feed;

public interface IFeedService
{
	IAsyncEnumerable<IMediaDetail> GetFeedItemsAsync(string identifier, CancellationToken cancellationToken = default);
}
