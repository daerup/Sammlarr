using Domain.Identity;
using Domain.Jobs;

namespace Contracts.Jobs;

public interface IJobRepository
{
	Task<EnqueueResult> EnqueueAsync(QueuedJob job, CancellationToken cancellationToken = default);

	Task<int> RequeueRunningAsync(DateTimeOffset requeuedAt, CancellationToken cancellationToken = default);

	Task<Optional<RunningJob>> TakeNextAsync(DateTimeOffset now, CancellationToken cancellationToken = default);

	Task CompleteAsync(JobId id, DateTimeOffset completedAt, CancellationToken cancellationToken = default);
}

public readonly record struct QueuedJob(JobKind Kind, string Key, UserId Owner, DateTimeOffset RunAfter);

public readonly record struct RunningJob(JobId Id, JobKind Kind, string Key, UserId Owner);
