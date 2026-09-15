using Contracts.Media;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.Media;

internal class MediaModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddSingleton<MediaWriteLock>();
		services.AddScoped<IMediaRepository, MediaRepository>();
		services.AddScoped<IModelConfiguration, MediaRowConfiguration>();
	}
}
