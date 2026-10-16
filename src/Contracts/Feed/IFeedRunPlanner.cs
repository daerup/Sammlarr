using Domain.Feed.Planning;
using Domain.Media;

namespace Contracts.Feed;

public interface IFeedRunPlanner
{
	Task<FeedPlanResult> PlanAsync(FeedPlanTarget target, CancellationToken cancellationToken = default);

	Task<FieldNeeds> NeedsAsync(Domain.Feed.Feed feed, CancellationToken cancellationToken = default);
}
