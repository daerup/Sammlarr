using Contracts.Feed;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.Feed;

internal sealed class FeedModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<Run.FeedRunModule>();
		services.AddScoped<IFeedRepository, FeedRepository>();
		services.AddScoped<IModelConfiguration, FeedRowConfiguration>();
		services.AddScoped<IModelConfiguration, FeedAbsenceRowConfiguration>();
		services.AddScoped<IDeletedFeeds, DeletedFeedStore>();
		services.AddScoped<IModelConfiguration, DeletedFeedRowConfiguration>();
	}
}
