using Contracts.Media;
using Contracts.Media.Search;

namespace Contracts.Watchlist;

public interface IWatchlistService
{
	Task<WatchlistPageResult> GetWatchlistAsync(int page, CancellationToken cancellationToken = default);

	IAsyncEnumerable<IMediaDetailResult> GetWatchlistAsync(CancellationToken cancellationToken = default);

	Task<bool> AddToWatchlistAsync(IMediaId mediaId, CancellationToken cancellationToken = default);

	Task<bool> RemoveFromWatchlistAsync(IMediaId mediaId, CancellationToken cancellationToken = default);
}
