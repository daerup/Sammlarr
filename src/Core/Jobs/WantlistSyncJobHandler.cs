using Contracts.Jobs;
using Contracts.Wantlist.Sync;
using Domain.Jobs;
using Domain.Usage;
using Observability.Usage;

namespace Core.Jobs;

internal sealed class WantlistSyncJobHandler(IWantlistSyncEngine engine) : IJobHandler<SyncWantlist>
{
	public async Task HandleAsync(SyncWantlist request, CancellationToken cancellationToken = default)
	{
		using var marked = UsageCauses.Mark(UsageCause.WantlistSync);
		await engine.SyncAsync(request.Trigger, cancellationToken);
	}
}
