using Domain.Wantlist.Sync;

namespace Contracts.Wantlist.Sync;

public interface IWantlistSyncEngine
{
	Task<WantlistSyncResult> SyncAsync(WantlistSyncTrigger trigger, CancellationToken cancellationToken = default);
}
