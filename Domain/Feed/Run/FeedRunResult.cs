namespace Domain.Feed.Run;

public union FeedRunResult(FeedRunCompleted, FeedRunAlreadyRunning, FeedRunFailed);

public sealed record FeedRunCompleted(FeedDelta Delta, Conclusiveness Conclusiveness);

public readonly record struct FeedRunAlreadyRunning
{
	public override string ToString() => "This feed is already running.";
}

public readonly record struct FeedRunFailed(string Reason)
{
	public override string ToString() => $"The run could not finish - {Reason}";
}
