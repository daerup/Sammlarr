using DB.Data.Access;
using DB.Data.Blocklist;
using DB.Data.Cleanup;
using DB.Data.Config;
using DB.Data.Datasets;
using DB.Data.Feed;
using DB.Data.Identity;
using DB.Data.Jobs;
using DB.Data.Media;
using DB.Data.Notifications;
using DB.Data.Ratings;
using DB.Data.Scheduler;
using DB.Data.Usage;
using DB.Data.Wantlist;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data;

internal class DataAccessModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<AccessModule>();
		services.AddModule<BlocklistModule>();
		services.AddModule<CleanupModule>();
		services.AddModule<ConfigModule>();
		services.AddModule<DatasetsDataModule>();
		services.AddModule<FeedModule>();
		services.AddModule<IdentityDataModule>();
		services.AddModule<JobModule>();
		services.AddModule<MediaModule>();
		services.AddModule<NotificationDataModule>();
		services.AddModule<RatingsDataModule>();
		services.AddModule<SchedulerDataModule>();
		services.AddModule<UsageDataModule>();
		services.AddModule<WantlistModule>();
	}
}
