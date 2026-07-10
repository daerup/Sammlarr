using Contracts.Media;
using Domain.Media;
using Domain.Watchlist;

namespace Contracts.Watchlist;

public interface IWatchlistService
{
	IAsyncEnumerable<WatchlistedMedia> GetWatchlistItemsAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlySet<MediaId>> GetWatchlistIdsAsync(CancellationToken cancellationToken = default);

	Task AddToWatchlistAsync(IEnumerable<MediaId> mediaIds, WatchlistItemProvenance provenance, CancellationToken cancellationToken = default);

	Task RemoveFromWatchlistAsync(IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default);
}
