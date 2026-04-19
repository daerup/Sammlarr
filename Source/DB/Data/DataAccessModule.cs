using DB.Data.Access;
using DB.Data.Blacklist;
using DB.Data.Config;
using DB.Data.List;
using DB.Data.MediaGroup;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data;

internal class DataAccessModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<AccessModule>();
		services.AddModule<BlacklistModule>();
		services.AddModule<ConfigModule>();
		services.AddModule<ListModule>();
		services.AddModule<MediaGroupModule>();
	}
}
