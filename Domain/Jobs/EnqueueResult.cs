namespace Domain.Jobs;

public union EnqueueResult(Queued, AlreadyQueued);

public readonly record struct Queued(JobId Id);

public readonly record struct AlreadyQueued(JobId Id);
