using Domain.Watchlist.Sync;

namespace Contracts.Watchlist.Sync;

public interface IWatchlistSyncPlanner
{
	Task<WatchlistPlanResult> PlanAsync(CancellationToken cancellationToken = default);
}
