using Contracts.Media;
using Domain.Media;

namespace Contracts.Watchlist.Outbox;

public interface IWatchlistOutbox
{
	Task<IReadOnlyList<WatchlistOperation>> GetAsync(CancellationToken cancellationToken = default);

	Task EnqueueAsync(WatchlistOperationKind kind, IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default);

	Task DropAsync(WatchlistOperationKind kind, IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default);

	Task RecordFailureAsync(WatchlistOperationKind kind, IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default);
}
