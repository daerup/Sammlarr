using System.Reflection;
using Contracts;
using Contracts.Identity;
using Contracts.Jobs;
using Domain.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Core.Jobs;

internal sealed class JobRunner(IServiceScopeFactory scopes, JobSignal signal, IDateTimeProvider clock, ILogger<JobRunner> logger) : BackgroundService
{
	private static readonly MethodInfo Handle = typeof(JobRunner).GetMethod(nameof(HandleAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await using var shutdown = stoppingToken.Register(signal.Complete);

		await RequeueRunningAsync(stoppingToken);
		signal.Raise();

		await foreach (var _ in signal.NudgesAsync().WithCancellation(stoppingToken))
		{
			if (await TryRunNextAsync(stoppingToken))
			{
				signal.Raise();
			}
		}
	}

	internal async Task RequeueRunningAsync(CancellationToken cancellationToken)
	{
		await using var scope = scopes.CreateAsyncScope();
		var requeued = await scope.ServiceProvider.GetRequiredService<IJobRepository>().RequeueRunningAsync(clock.Now, cancellationToken);
		if (requeued > 0)
		{
			logger.LogWarning("Requeued {Count} jobs a previous process left running", requeued);
		}
	}

	internal async Task<bool> TryRunNextAsync(CancellationToken cancellationToken)
	{
		await using var scope = scopes.CreateAsyncScope();
		var jobs = scope.ServiceProvider.GetRequiredService<IJobRepository>();
		if (await jobs.TakeNextAsync(clock.Now, cancellationToken) is not Some<RunningJob>(var job))
		{
			return false;
		}

		using var _ = await scope.ServiceProvider.GetRequiredService<IUserScope>().AsAsync(job.Owner, cancellationToken);
		var request = job.Request;
		try
		{
			await DispatchAsync(scope.ServiceProvider, request, cancellationToken);
			await jobs.CompleteAsync(job.Id, clock.Now, cancellationToken);
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Job {Kind} {Key} failed", job.Request.Kind, job.Request.Key);
			await jobs.FailAsync(job.Id, clock.Now, cancellationToken);
		}

		return true;
	}

	internal static async Task DispatchAsync(IServiceProvider services, IJobRequest request, CancellationToken cancellationToken) =>
		await (Task)Handle.MakeGenericMethod(request.GetType()).Invoke(null, [services, request, cancellationToken])!;

	private static async Task HandleAsync<TRequest>(IServiceProvider services, IJobRequest request, CancellationToken cancellationToken)
		where TRequest : IJobRequest => await services.GetRequiredService<IJobHandler<TRequest>>().HandleAsync((TRequest)request, cancellationToken);
}
