namespace Domain.Jobs;

public readonly union EnqueueResult(Queued, AlreadyQueued);

public readonly record struct Queued(JobId Id, JobState State);

public readonly record struct AlreadyQueued(JobId Id, JobState State);
