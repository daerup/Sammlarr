using Domain.Feed;

namespace Contracts.Feed;

public interface IFeedRepository
{
	Task<IReadOnlyList<Domain.Feed.Feed>> GetFeedsAsync(CancellationToken cancellationToken = default);
	Task<Domain.Feed.Feed?> GetFeedByIdAsync(FeedId id, CancellationToken cancellationToken = default);
	Task<FeedId> CreateFeedAsync(string name, FeedCreateData createData, CancellationToken cancellationToken = default);
	Task UpdateFeedAsync(FeedId id, FeedUpdateData updateData, CancellationToken cancellationToken = default);
	Task DeleteFeedAsync(FeedId id, CancellationToken cancellationToken = default);
}
