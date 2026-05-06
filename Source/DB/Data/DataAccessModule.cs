using DB.Data.Access;
using DB.Data.Blacklist;
using DB.Data.Config;
using DB.Data.Feed;
using DB.Data.MediaGroup;
using DB.Data.Metadata;
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
		services.AddModule<FeedModule>();
		services.AddModule<MediaGroupModule>();
		services.AddModule<MetadataModule>();
	}
}
