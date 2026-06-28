namespace Contracts.Feed;

public interface IFeedService
{
	IAsyncEnumerable<MediaLookup> GetFeedItemsAsync(string identifier, CancellationToken cancellationToken = default);
}
