using Contracts.Blacklist;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.Blacklist;

internal class BlacklistModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<IBlacklistRepository, BlacklistRepository>();
		services.AddScoped<IModelConfiguration, BlacklistRowConfiguration>();
	}
}
