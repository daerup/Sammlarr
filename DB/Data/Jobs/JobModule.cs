using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.Jobs;

internal class JobModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<IModelConfiguration, JobRowConfiguration>();
	}
}
