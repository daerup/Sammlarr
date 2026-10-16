using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.Feed.Run;

internal sealed class FeedRunModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<IModelConfiguration, FeedRunRowConfiguration>();
	}
}
