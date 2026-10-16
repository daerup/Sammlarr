using Domain.Wantlist.Sync;

namespace Contracts.Wantlist.Sync;

public interface IWantlistSyncPlanner
{
	Task<WantlistPlanResult> PlanAsync(CancellationToken cancellationToken = default);
}
