using Contracts.Media;

namespace Contracts.Watchlist.Outbox;

public interface IWatchlistOutbox
{
	Task<IReadOnlyList<WatchlistOperation>> GetAsync(MediaProvider mediaProvider, CancellationToken cancellationToken = default);

	Task EnqueueAsync(WatchlistOperationKind kind, IEnumerable<IMediaId> mediaIds, CancellationToken cancellationToken = default);

	Task DropAsync(WatchlistOperationKind kind, IEnumerable<IMediaId> mediaIds, CancellationToken cancellationToken = default);
}
