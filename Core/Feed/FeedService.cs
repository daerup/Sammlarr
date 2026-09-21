using System.Runtime.CompilerServices;
using Contracts.DI;
using Contracts.Feed;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Providers;

namespace Core.Feed;

internal sealed class FeedService(IFeedRepository repository, IKeyedServiceIndex<FeedProvider, IFeedProvider> providers) : IFeedService
{
	public Task<IReadOnlyList<Domain.Feed.Feed>> GetFeedsAsync(CancellationToken cancellationToken = default) => repository.GetFeedsAsync(cancellationToken);

	public Task<Domain.Feed.Feed?> GetFeedByIdAsync(FeedId id, CancellationToken cancellationToken = default) =>
		repository.GetFeedByIdAsync(id, cancellationToken);

	public Task<FeedId> CreateFeedAsync(FeedCreateData createData, CancellationToken cancellationToken = default) =>
		repository.CreateFeedAsync(createData, cancellationToken);

	public Task UpdateFeedAsync(FeedId id, FeedUpdateData updateData, CancellationToken cancellationToken = default) =>
		repository.UpdateFeedAsync(id, updateData, cancellationToken);

	public Task DeleteFeedAsync(FeedId id, CancellationToken cancellationToken = default) => repository.DeleteFeedAsync(id, cancellationToken);

	public async IAsyncEnumerable<MediaLookup> GetFeedItemsAsync(Domain.Feed.Feed feed, [EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		await foreach (var item in providers[feed.Provider].GetFeedItemsAsync(feed.Identifier, cancellationToken))
		{
			yield return item;
		}
	}

	public Task<IReadOnlyList<FeedRun>> GetRunsAsync(FeedId feedId, int take, CancellationToken cancellationToken = default) =>
		repository.GetRunsAsync(feedId, take, cancellationToken);

	public Task<Optional<FeedRun>> GetRunAsync(Guid runId, CancellationToken cancellationToken = default) => repository.GetRunAsync(runId, cancellationToken);

	public Task<FeedRunHistory> GetHistoryAsync(FeedId feedId, CancellationToken cancellationToken = default) =>
		repository.GetHistoryAsync(feedId, cancellationToken);

	public Task<IReadOnlyDictionary<FeedId, FeedOverview>> GetOverviewsAsync(CancellationToken cancellationToken = default) =>
		repository.GetOverviewsAsync(cancellationToken);
}
