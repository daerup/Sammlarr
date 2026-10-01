namespace Contracts.Jobs;

public interface IJobRunnerStatus
{
	bool HasStopped { get; }

	int ConsecutiveFailures { get; }

	DateTimeOffset? LastFailureAt { get; }
}
