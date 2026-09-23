namespace Domain.Feed.Planning;

public union FeedPlanResult(IFeedPlan, FeedPlanFailed);

public readonly record struct FeedPlanFailed(string Reason)
{
	public override string ToString() => $"The preview could not finish - {Reason}";
}
