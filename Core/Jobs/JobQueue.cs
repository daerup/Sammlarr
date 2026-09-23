using Contracts;
using Contracts.Identity;
using Contracts.Jobs;
using Domain.Jobs;

namespace Core.Jobs;

internal sealed class JobQueue(IJobRepository jobs, JobSignal signal, ICurrentUser currentUser, IDateTimeProvider clock) : IJobQueue
{
	public async Task<EnqueueResult> EnqueueAsync(IJobRequest request, CancellationToken cancellationToken = default)
	{
		var result = await jobs.EnqueueAsync(new QueuedJob(request, currentUser.Id, clock.Now), cancellationToken);
		signal.Raise();
		return result;
	}
}
