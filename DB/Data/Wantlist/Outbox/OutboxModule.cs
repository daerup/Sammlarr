using Contracts.Wantlist.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.Wantlist.Outbox;

internal class OutboxModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<IWantlistOutbox, WantlistOutboxRepository>();
		services.AddScoped<IModelConfiguration, WantlistOutboxRowConfiguration>();
	}
}
