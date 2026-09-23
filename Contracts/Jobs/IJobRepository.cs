using Domain.Identity;
using Domain.Jobs;

namespace Contracts.Jobs;

public interface IJobRepository
{
	Task<EnqueueResult> EnqueueAsync(QueuedJob job, CancellationToken cancellationToken = default);
}

public readonly record struct QueuedJob(JobKind Kind, string Key, UserId Owner, DateTimeOffset RunAfter);
