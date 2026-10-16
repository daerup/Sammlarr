using Contracts.Feed;
using Domain.Feed;

namespace Harness;

internal sealed class RecordingFeedScheduler : IFeedScheduler
{
	private readonly List<(FeedId Id, Scheduled Schedule)> _scheduled = [];
	private readonly List<FeedId> _unscheduled = [];

	public IReadOnlyList<(FeedId Id, Scheduled Schedule)> Scheduled => _scheduled;

	public IReadOnlyList<FeedId> Unscheduled => _unscheduled;

	public Task ScheduleAsync(FeedId id, Scheduled schedule, CancellationToken cancellationToken = default)
	{
		_scheduled.Add((id, schedule));
		return Task.CompletedTask;
	}

	public Task UnscheduleAsync(FeedId id, CancellationToken cancellationToken = default)
	{
		_unscheduled.Add(id);
		return Task.CompletedTask;
	}
}
