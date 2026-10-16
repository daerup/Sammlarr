using Domain.Jobs;

namespace Contracts.Jobs;

public interface IJobQueueView
{
	Task<QueueSnapshot> GetAsync(CancellationToken cancellationToken = default);
}

public readonly record struct QueueSnapshot(IReadOnlyList<PendingJob> Jobs, int OthersPending);

public readonly record struct PendingJob(JobId Id, IJobRequest Request, JobState State, DateTimeOffset RunAfter, int Attempts);
