using Contracts.Media;

namespace Contracts.Watchlist;

public interface IWatchlistService
{
	Task<WatchlistPageResult> GetWatchlistAsync(int page, CancellationToken cancellationToken = default);

	Task<bool> AddToWatchlistAsync(IMediaId ratingKey, CancellationToken cancellationToken = default);
	Task<bool> RemoveFromWatchlistAsync(IMediaId ratingKey, CancellationToken cancellationToken = default);
}
