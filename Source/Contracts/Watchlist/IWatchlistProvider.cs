using Contracts.Media;

namespace Contracts.Watchlist;

public interface IWatchlistProvider
{
	Task<WatchlistProviderPage> GetWatchlistAsync(int page, CancellationToken cancellationToken = default);

	Task<bool> AddToWatchlistAsync(IMediaId mediaId, CancellationToken cancellationToken = default);

	Task<bool> RemoveFromWatchlistAsync(IMediaId mediaId, CancellationToken cancellationToken = default);
}

public record WatchlistProviderPage(IReadOnlyList<IMediaId> Items, int TotalPages);
