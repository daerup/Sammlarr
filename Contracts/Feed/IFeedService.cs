using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Providers;

namespace Contracts.Feed;

public interface IFeedService
{
	Task<IReadOnlyList<Domain.Feed.Feed>> GetFeedsAsync(CancellationToken cancellationToken = default);

	Task<Domain.Feed.Feed?> GetFeedByIdAsync(FeedId id, CancellationToken cancellationToken = default);

	Task<FeedId> CreateFeedAsync(FeedCreateData createData, CancellationToken cancellationToken = default);

	Task UpdateFeedAsync(FeedId id, FeedUpdateData updateData, CancellationToken cancellationToken = default);

	Task DeleteFeedAsync(FeedId id, CancellationToken cancellationToken = default);

	Task DisableFeedsUsingAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default);

	Task SetEnabledAsync(IReadOnlyCollection<FeedId> ids, bool enabled, CancellationToken cancellationToken = default);

	IAsyncEnumerable<MediaLookup> GetFeedItemsAsync(Domain.Feed.Feed feed, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<FeedRun>> GetRunsAsync(FeedId feedId, int take, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<FeedRun>> GetRecentRunsAsync(DateTimeOffset since, int take, CancellationToken cancellationToken = default);

	Task<int> CountRunsSinceAsync(FeedId feedId, DateTimeOffset since, CancellationToken cancellationToken = default);

	Task<Optional<FeedRun>> GetRunAsync(Guid runId, CancellationToken cancellationToken = default);

	Task<FeedRunHistory> GetHistoryAsync(FeedId feedId, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<FeedId, FeedOverview>> GetOverviewsAsync(CancellationToken cancellationToken = default);
}
