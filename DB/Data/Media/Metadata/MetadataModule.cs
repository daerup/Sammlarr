using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.Media.Metadata;

internal class MetadataModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<MediaMetadataFactory>();
		services.AddScoped<IModelConfiguration, MediaMetadataRowConfiguration>();
	}
}
