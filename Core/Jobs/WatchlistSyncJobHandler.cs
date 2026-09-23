using Contracts.Jobs;
using Contracts.Watchlist.Sync;
using Domain.Jobs;

namespace Core.Jobs;

internal sealed class WatchlistSyncJobHandler(IWatchlistSyncEngine engine) : IJobHandler<SyncWatchlist>
{
	public async Task HandleAsync(SyncWatchlist request, CancellationToken cancellationToken = default) =>
		await engine.SyncAsync(request.Trigger, cancellationToken);
}
