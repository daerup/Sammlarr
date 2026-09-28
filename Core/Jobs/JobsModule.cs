using Contracts.Jobs;
using Domain.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Observability.Tracing;
using Shared.DI;

namespace Core.Jobs;

internal class JobsModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddSingleton<JobSignal>();
		services.AddSingleton<JobAlarm>();
		services.AddSingleton<JobRunnerStatus>();
		services.AddSingleton<IJobRunnerStatus>(sp => sp.GetRequiredService<JobRunnerStatus>());
		services.AddScoped<IJobHandler<RunFeed>, FeedRunJobHandler>();
		services.AddScoped<IJobHandler<SyncWatchlist>, WatchlistSyncJobHandler>();
		services.AddTracedScoped<IJobQueue, JobQueue>();
		services.AddHostedService<JobRunner>();
	}
}
