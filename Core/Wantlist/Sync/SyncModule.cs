using Contracts.Wantlist.Sync;
using Microsoft.Extensions.DependencyInjection;
using Observability.Tracing;
using Shared.DI;

namespace Core.Wantlist.Sync;

internal class SyncModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddSingleton<WantlistSyncGate>();
		services.AddTracedScoped<IWantlistSyncPlanner, WantlistSyncPlanner>();
		services.AddTracedScoped<IWantlistSyncEngine, WantlistSyncEngine>();
	}
}
