using Domain.Feed;
using Domain.Media;
using Domain.Watchlist;

namespace Contracts.Watchlist;

public interface IWatchlistRepository
{
	Task<IReadOnlyList<WatchlistEntry>> GetEntriesAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyList<MediaId>> GetIdsAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<FeedId, int>> GetOwnedCountsAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyList<MediaId>> AddAsync(IEnumerable<MediaId> mediaIds, WatchlistItemProvenance provenance, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<MediaId>> RemoveAsync(IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<MediaId>> RemoveOwnedAsync(IEnumerable<MediaId> mediaIds, FeedId owner, CancellationToken cancellationToken = default);
}
