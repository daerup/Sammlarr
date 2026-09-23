namespace Domain.Jobs;

public enum JobState
{
	Queued,
	Running,
	Done,
	Failed,
}
