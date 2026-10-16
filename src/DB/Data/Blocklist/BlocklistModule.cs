using Contracts.Blocklist;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.Blocklist;

internal sealed class BlocklistModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<IBlocklistRepository, BlocklistRepository>();
		services.AddScoped<IModelConfiguration, BlocklistRowConfiguration>();
	}
}
