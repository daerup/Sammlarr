using Contracts;
using Contracts.Identity;
using Contracts.Jobs;
using Domain.Jobs;

namespace Core.Jobs;

internal sealed class JobQueue(IJobRepository jobs, ICurrentUser currentUser, IDateTimeProvider clock) : IJobQueue
{
	public async Task<EnqueueResult> EnqueueAsync(JobRequest request, CancellationToken cancellationToken = default) =>
		await jobs.EnqueueAsync(new QueuedJob(request.Kind, request.Key, currentUser.Id, clock.Now), cancellationToken);
}
