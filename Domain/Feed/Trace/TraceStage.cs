namespace Domain.Feed.Trace;

public enum TraceStage
{
	Ids,
	Details,
}

public enum HopOutcome
{
	Answered,
	AfterRateLimit,
	GaveUp,
	Failed,
}

public enum TitleFate
{
	Matched,
	CouldNotLookUp,
	NotFound,
	Conflict,
}

public enum TraceEnding
{
	Completed,
	Failed,
}
