namespace Domain.Usage;

public enum UsageOutcome
{
	Answered,
	AnsweredAfter429,
	GaveUpOn429,
	Failed,
	Cancelled,
}

public enum UsageFault
{
	None,
	TimedOut,
	Cancelled,
	Other,
}

public static class UsageOutcomes
{
	private const int TooManyRequests = 429;

	private const int NotFound = 404;

	public static UsageOutcome Classify(Optional<int> status, UsageFault fault, int rateLimited)
	{
		var answered = status.Match(code => code is >= 200 and < 400 or NotFound, () => false);
		var limitedAtTheEnd = status.Match(code => code == TooManyRequests, () => false);
		return (fault, answered) switch
		{
			(UsageFault.Cancelled, _) => UsageOutcome.Cancelled,
			(_, true) when rateLimited > 0 => UsageOutcome.AnsweredAfter429,
			(_, true) => UsageOutcome.Answered,
			_ when limitedAtTheEnd || (fault == UsageFault.TimedOut && rateLimited > 0) => UsageOutcome.GaveUpOn429,
			_ => UsageOutcome.Failed,
		};
	}
}
