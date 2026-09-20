using Domain.Media;

namespace Contracts.Watchlist;

public interface IWatchlistProvider
{
	IAsyncEnumerable<UnmatchedMedia> GetWatchlistAsync(CancellationToken cancellationToken = default);

	IAsyncEnumerable<WatchlistPushAck> AddToWatchlistAsync(IEnumerable<IExternalMediaId> ids, CancellationToken cancellationToken = default);

	IAsyncEnumerable<WatchlistPushAck> RemoveFromWatchlistAsync(IEnumerable<IExternalMediaId> ids, CancellationToken cancellationToken = default);
}

public readonly record struct WatchlistPushAck(IExternalMediaId ExternalId, bool Success);
