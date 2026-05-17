using Contracts.Media;

namespace Contracts.Watchlist;

public interface IWatchlistProvider
{
	IAsyncEnumerable<IMediaId> GetWatchlistAsync(CancellationToken cancellationToken = default);

	Task<bool> AddToWatchlistAsync(IMediaId mediaId, CancellationToken cancellationToken = default);

	Task<bool> RemoveFromWatchlistAsync(IMediaId mediaId, CancellationToken cancellationToken = default);
}
