using Contracts.Identity;
using Domain.Watchlist.Sync;

namespace Contracts.Watchlist.Sync;

public interface IWatchlistSyncEngine
{
	Task<WatchlistSyncResult> SyncAsync(WatchlistSyncTrigger trigger, ICurrentUser user, CancellationToken cancellationToken = default);

	Task<WatchlistSyncResult> DryRunAsync(ICurrentUser user, CancellationToken cancellationToken = default);
}
