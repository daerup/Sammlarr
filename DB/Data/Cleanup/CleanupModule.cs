using Contracts.Media.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.Cleanup;

internal class CleanupModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<IMediaCachePurger, MediaCachePurger>();
	}
}
