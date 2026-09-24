namespace Contracts.Jobs;

public interface IJobRunnerStatus
{
	bool IsRunning { get; }

	bool HasStopped { get; }

	int ConsecutiveFailures { get; }

	DateTimeOffset? LastFailureAt { get; }
}
