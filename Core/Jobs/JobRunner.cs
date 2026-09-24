using System.Reflection;
using Contracts;
using Contracts.Identity;
using Contracts.Jobs;
using Domain.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Core.Jobs;

internal sealed class JobRunner(IServiceScopeFactory scopes, JobSignal signal, JobRunnerStatus status, IDateTimeProvider clock, ILogger<JobRunner> logger)
	: BackgroundService
{
	private static readonly MethodInfo Handle = typeof(JobRunner).GetMethod(nameof(HandleAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await using var shutdown = stoppingToken.Register(signal.Complete);

		using var running = status.Running();

		try
		{
			await GuardAsync(() => RequeueRunningAsync(stoppingToken), "Jobs left running by a previous process could not be recovered", stoppingToken);
			signal.Raise();

			await foreach (var _ in signal.NudgesAsync().WithCancellation(stoppingToken))
			{
				await GuardAsync(() => PumpAsync(stoppingToken), "The job runner could not take the next job and will retry when one is enqueued", stoppingToken);
			}
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
		catch (Exception exception)
		{
			logger.LogCritical(exception, "The job runner stopped and no queued work will run until the app restarts");
		}
	}

	private async Task GuardAsync(Func<Task> work, string whatFailed, CancellationToken cancellationToken)
	{
		try
		{
			await work();
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception exception)
		{
			status.Failed(clock.Now);
			logger.LogError(exception, whatFailed);
		}
	}

	private async Task PumpAsync(CancellationToken cancellationToken)
	{
		var ran = await TryRunNextAsync(cancellationToken);
		status.Succeeded();
		if (ran)
		{
			signal.Raise();
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
			await jobs.CompleteAsync(job.Id, clock.Now, CancellationToken.None);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			logger.LogInformation("Job {Kind} {Key} was interrupted by shutdown and stays running for recovery", request.Kind, request.Key);
			throw;
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Job {Kind} {Key} failed", request.Kind, request.Key);
			await jobs.FailAsync(job.Id, clock.Now, CancellationToken.None);
		}

		return true;
	}

	internal static async Task DispatchAsync(IServiceProvider services, IJobRequest request, CancellationToken cancellationToken) =>
		await (Task)Handle.MakeGenericMethod(request.GetType()).Invoke(null, [services, request, cancellationToken])!;

	private static async Task HandleAsync<TRequest>(IServiceProvider services, IJobRequest request, CancellationToken cancellationToken)
		where TRequest : IJobRequest => await services.GetRequiredService<IJobHandler<TRequest>>().HandleAsync((TRequest)request, cancellationToken);
}
