using Contracts.Media;

namespace Contracts.Watchlist;

public interface IWatchlistService
{
	IAsyncEnumerable<WatchlistItem> GetWatchlistItemsAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlySet<MediaId>> GetWatchlistIdsAsync(CancellationToken cancellationToken = default);

	Task AddToWatchlistAsync(IEnumerable<MediaId> mediaIds, WatchlistItemProvenance provenance, CancellationToken cancellationToken = default);

	Task RemoveFromWatchlistAsync(IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default);
}
