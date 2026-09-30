using Contracts.Jobs;
using Contracts.Wantlist.Sync;
using Domain.Jobs;

namespace Core.Jobs;

internal sealed class WantlistSyncJobHandler(IWantlistSyncEngine engine) : IJobHandler<SyncWantlist>
{
	public async Task HandleAsync(SyncWantlist request, CancellationToken cancellationToken = default) =>
		await engine.SyncAsync(request.Trigger, cancellationToken);
}
