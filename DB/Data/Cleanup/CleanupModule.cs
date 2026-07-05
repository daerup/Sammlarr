using Contracts.Media.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Observability.Tracing;
using Shared.DI;

namespace DB.Data.Cleanup;

internal class CleanupModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddTracedScoped<IMediaCachePurger, MediaCachePurger>();
	}
}
