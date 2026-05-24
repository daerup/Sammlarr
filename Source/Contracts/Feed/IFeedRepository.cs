namespace Contracts.Feed;

public interface IFeedRepository
{
	Task<IReadOnlyList<Feed>> GetFeedsAsync(CancellationToken cancellationToken = default);
	Task<Feed?> GetFeedByIdAsync(Guid id, CancellationToken cancellationToken = default);
	Task CreateFeedAsync(string name, FeedCreateData createData, CancellationToken cancellationToken = default);
	Task UpdateFeedAsync(Guid id, FeedUpdateData updateData, CancellationToken cancellationToken = default);
	Task DeleteFeedAsync(Guid id, CancellationToken cancellationToken = default);
}
