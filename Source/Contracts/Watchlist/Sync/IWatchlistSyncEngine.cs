namespace Contracts.Watchlist.Sync;

public interface IWatchlistSyncEngine
{
	Task<WatchlistSyncResult> SyncAsync(WatchlistSyncTrigger trigger, CancellationToken cancellationToken = default);

	Task<WatchlistSyncDryRun> DryRunAsync(CancellationToken cancellationToken = default);
}
