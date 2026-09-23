using Contracts.Blacklist;
using Microsoft.Extensions.DependencyInjection;
using Observability.Tracing;
using Shared.DI;

namespace Core.Blacklist;

internal class BlacklistModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddTracedScoped<IBlacklistService, BlacklistService>();
	}
}
