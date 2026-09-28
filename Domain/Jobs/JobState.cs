namespace Domain.Jobs;

public enum JobState
{
	Waiting,
	Queued,
	Running,
	Done,
	Failed,
}
