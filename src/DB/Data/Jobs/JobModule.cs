using Contracts.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.Jobs;

internal sealed class JobModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<IModelConfiguration, JobRowConfiguration>();
		services.AddScoped<IJobRepository, JobRepository>();
		services.AddScoped<IJobQueueView, JobQueueView>();
	}
}
