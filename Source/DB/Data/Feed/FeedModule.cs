using Contracts.Media.Feed;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.Feed;

internal class FeedModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services) => services.AddScoped<IFeedRepository, FeedRepository>();
}
