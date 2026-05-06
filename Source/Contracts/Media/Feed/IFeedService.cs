using Contracts.Media.Search;

namespace Contracts.Media.Feed;

public interface IFeedService
{
	IAsyncEnumerable<IMediaDetailResult> GetFeedItemsAsync(string identifier, CancellationToken cancellationToken = default);
}
