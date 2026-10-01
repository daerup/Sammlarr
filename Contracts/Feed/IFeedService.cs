using Domain.Feed;
using Domain.Feed.Run;
using Domain.Providers;

namespace Contracts.Feed;

public interface IFeedService
{
	Task<FeedSnapshot> GetAsync(CancellationToken cancellationToken = default);

	Task<FeedId> CreateAsync(FeedCreateData data, CancellationToken cancellationToken = default);

	Task UpdateAsync(FeedId id, FeedUpdateData data, CancellationToken cancellationToken = default);

	Task DeleteAsync(FeedId id, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<FeedId>> PauseAsync(IReadOnlyCollection<FeedId> ids, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<FeedId>> ResumeAsync(IReadOnlyCollection<FeedId> ids, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<FeedId>> PauseUsingAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<FeedRun>> GetRunsAsync(RunQuery query, CancellationToken cancellationToken = default);
}
