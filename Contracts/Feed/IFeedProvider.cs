using Domain.Media;

namespace Contracts.Feed;

public interface IFeedProvider
{
	Task<Result<string>> NormalizeAsync(string input, CancellationToken cancellationToken = default);

	IAsyncEnumerable<Listed<MediaMatchResult>> GetFeedItemsAsync(string identifier, FieldNeeds needs, CancellationToken cancellationToken = default);
}
