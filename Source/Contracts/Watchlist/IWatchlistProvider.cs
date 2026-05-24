using Contracts.Media;

namespace Contracts.Watchlist;

public interface IWatchlistProvider
{
	IAsyncEnumerable<IExternalMediaId> GetWatchlistAsync(CancellationToken cancellationToken = default);

	IAsyncEnumerable<WatchlistPushResult> AddToWatchlistAsync(IEnumerable<IExternalMediaId> ids, CancellationToken cancellationToken = default);

	IAsyncEnumerable<WatchlistPushResult> RemoveFromWatchlistAsync(IEnumerable<IExternalMediaId> ids, CancellationToken cancellationToken = default);
}

public readonly record struct WatchlistPushResult(IExternalMediaId ExternalId, bool Success);

