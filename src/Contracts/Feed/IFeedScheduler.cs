using Domain.Feed;

namespace Contracts.Feed;

public interface IFeedScheduler
{
	Task ScheduleAsync(FeedId id, Scheduled schedule, CancellationToken cancellationToken = default);

	Task UnscheduleAsync(FeedId id, CancellationToken cancellationToken = default);
}
