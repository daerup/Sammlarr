using Domain.Feed;
using Domain.Feed.Run;
using Domain.Providers;

namespace Contracts.Feed;

public interface IFeedService
{
	Task<FeedSnapshot> GetAsync(CancellationToken cancellationToken = default);

	Task<Result<FeedId>> CreateAsync(FeedCreateData data, CancellationToken cancellationToken = default);

	Task<Result<FeedId>> UpdateAsync(FeedId id, FeedUpdateData data, CancellationToken cancellationToken = default);

	Task DeleteAsync(FeedId id, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<FeedId>> PauseAsync(IReadOnlyCollection<FeedId> ids, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<FeedId>> PauseAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<FeedId>> ResumeAsync(IReadOnlyCollection<FeedId> ids, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<FeedRun>> GetRunsAsync(RunQuery query, CancellationToken cancellationToken = default);
}
