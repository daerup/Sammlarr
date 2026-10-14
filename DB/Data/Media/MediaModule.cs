using Contracts.Media;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.Media;

internal sealed class MediaModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<IModelConfiguration, MediaDetailsRowConfiguration>();
		services.AddSingleton<MediaWriteLock>();
		services.AddScoped<IMediaRepository, MediaRepository>();
		services.AddScoped<IIdentityClaims, IdentityClaims>();
		services.AddScoped<IIdentityConflicts, IdentityConflictRepository>();
		services.AddScoped<IMediaMerge, MediaMerge>();
		services.AddScoped<IModelConfiguration, IdentityConflictConfiguration>();
		services.AddScoped<IModelConfiguration, MediaRowConfiguration>();
	}
}
