using Contracts.Media;
using Microsoft.Extensions.DependencyInjection;
using Observability.Tracing;
using Shared.DI;

namespace DB.Data.Cleanup;

internal sealed class CleanupModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddTracedScoped<IMediaCachePurger, MediaCachePurger>();
	}
}
