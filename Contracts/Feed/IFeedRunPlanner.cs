using Domain.Feed.Planning;

namespace Contracts.Feed;

public interface IFeedRunPlanner
{
	Task<FeedPlanResult> PlanAsync(FeedPlanTarget target, CancellationToken cancellationToken = default);
}
