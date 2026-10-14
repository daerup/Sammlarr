using Contracts.Wantlist;
using DB.Data.Wantlist.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.Wantlist;

internal sealed class WantlistModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<OutboxModule>();
		services.AddScoped<IWantlistRepository, WantlistRepository>();
		services.AddScoped<IModelConfiguration, WantlistRowConfiguration>();
	}
}
