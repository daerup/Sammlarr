using Contracts.Blocklist;
using Microsoft.Extensions.DependencyInjection;
using Observability.Tracing;
using Shared.DI;

namespace Core.Blocklist;

internal sealed class BlocklistModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddTracedScoped<IBlocklistService, BlocklistService>();
	}
}
