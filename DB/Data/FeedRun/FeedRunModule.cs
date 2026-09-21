using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.FeedRun;

internal class FeedRunModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<IModelConfiguration, FeedRunRowConfiguration>();
	}
}
