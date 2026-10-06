using Domain.Media;

namespace Contracts.Feed;

public interface IFeedProvider
{
	Task<Result<string>> NormalizeAsync(string input, CancellationToken cancellationToken = default);

	IAsyncEnumerable<MediaMatchResult> GetFeedItemsAsync(string identifier, CancellationToken cancellationToken = default);
}
