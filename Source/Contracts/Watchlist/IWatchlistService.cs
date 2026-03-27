using Contracts.Media;

namespace Contracts.Watchlist;

public interface IWatchlistService
{
	Task<WatchlistPageResult> GetWatchlistAsync(int page, CancellationToken cancellationToken = default);

	Task<bool> AddToWatchlistAsync(PlexId ratingKey, CancellationToken cancellationToken = default);
	Task<bool> RemoveFromWatchlistAsync(PlexId ratingKey, CancellationToken cancellationToken = default);
}
