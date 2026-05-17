using Contracts.Media;

namespace Contracts.Watchlist;

public interface IWatchlistService
{
	IAsyncEnumerable<WatchlistedItem> GetWatchlistItemsAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlySet<IMediaId>> GetWatchlistIdsAsync(CancellationToken cancellationToken = default);

	Task AddToWatchlistAsync(IEnumerable<IMediaId> mediaIds, WatchlistItemProvenance provenance, CancellationToken cancellationToken = default);

	Task RemoveFromWatchlistAsync(IMediaId mediaId, CancellationToken cancellationToken = default);
}
