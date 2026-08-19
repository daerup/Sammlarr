using Domain.Media;
using Domain.Watchlist.Outbox;

namespace Contracts.Watchlist.Outbox;

public interface IWatchlistOutbox
{
	Task<IReadOnlyList<WatchlistDelivery>> GetAsync(CancellationToken cancellationToken = default);

	Task EnqueueAsync(WatchlistOperationAction action, IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default);

	Task<WatchlistDelivery?> FindPendingAsync(MediaId mediaId, CancellationToken cancellationToken = default);

	Task DropAsync(IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default);

	Task RecordFailureAsync(IEnumerable<MediaId> mediaIds, WatchlistDeliveryStatus status, CancellationToken cancellationToken = default);
}
