using DB.Data.Access;
using DB.Data.Blacklist;
using DB.Data.Cleanup;
using DB.Data.Config;
using DB.Data.Feed;
using DB.Data.Media;
using DB.Data.Metadata;
using DB.Data.Watchlist;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data;

internal class DataAccessModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<AccessModule>();
		services.AddModule<BlacklistModule>();
		services.AddModule<CleanupModule>();
		services.AddModule<ConfigModule>();
		services.AddModule<FeedModule>();
		services.AddModule<MediaModule>();
		services.AddModule<MetadataModule>();
		services.AddModule<WatchlistModule>();
	}
}
