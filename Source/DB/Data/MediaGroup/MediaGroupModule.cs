using Contracts.Media;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.MediaGroup;

internal class MediaGroupModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<IMediaIdGroupRepository, MediaIdGroupRepository>();
	}
}
