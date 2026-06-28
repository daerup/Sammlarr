namespace Contracts.Feed;

public interface IFeedService
{
	IAsyncEnumerable<MediaMatch> GetFeedItemsAsync(string identifier, CancellationToken cancellationToken = default);
}
