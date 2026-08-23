using Domain.Media;
using Domain.Watchlist.Outbox;

namespace Contracts.Watchlist.Outbox;

public interface IWatchlistOutbox
{
	Task<IReadOnlyList<WatchlistOutboxEntry>> GetAsync(CancellationToken cancellationToken = default);

	Task EnqueueAsync(WatchlistOperationAction action, IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default);

	Task<WatchlistOutboxEntry?> FindPendingAsync(MediaId mediaId, CancellationToken cancellationToken = default);

	Task DropAsync(IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default);

	Task RecordFailedAttemptAsync(IEnumerable<MediaId> mediaIds, WatchlistOutboxState resultingState, CancellationToken cancellationToken = default);
}
