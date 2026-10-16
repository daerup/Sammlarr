using Contracts.Wantlist;
using Core.Wantlist.Sync;
using Microsoft.Extensions.DependencyInjection;
using Observability.Tracing;
using Shared.DI;

namespace Core.Wantlist;

internal sealed class WantlistModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<SyncModule>();
		services.AddTracedScoped<IWantlistService, WantlistService>();
	}
}
