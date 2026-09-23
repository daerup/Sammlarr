using Domain.Watchlist.Sync;

namespace Contracts.Watchlist.Sync;

public interface IWatchlistSyncEngine
{
	Task<WatchlistSyncResult> SyncAsync(WatchlistSyncTrigger trigger, CancellationToken cancellationToken = default);

	Task<WatchlistDryRunResult> DryRunAsync(CancellationToken cancellationToken = default);
}
