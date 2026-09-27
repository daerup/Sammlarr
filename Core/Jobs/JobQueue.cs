using Contracts;
using Contracts.Identity;
using Contracts.Jobs;
using Core.Announcing;
using Domain.Jobs;
using Domain.Watchlist.Sync;
using Shared.Options;

namespace Core.Jobs;

internal sealed class JobQueue(
	IJobRepository jobs,
	JobSignal signal,
	ICurrentUser currentUser,
	IDateTimeProvider clock,
	SchedulerOptions options,
	Announcer announcer
) : IJobQueue
{
	public async Task<EnqueueResult> EnqueueAsync(IJobRequest request, CancellationToken cancellationToken = default)
	{
		var (delay, maxWait) = Arming(request);
		var result = await jobs.EnqueueAsync(new QueuedJob(request, currentUser.Id, clock.Now + delay, maxWait), cancellationToken);
		signal.Raise();
		announcer.Announce(new JobStateChangedEvent(currentUser.Id, request.Kind, request.Key, JobState.Queued));
		return result;
	}

	private (TimeSpan Delay, TimeSpan MaxWait) Arming(IJobRequest request) =>
		request switch
		{
			SyncWatchlist(ScheduledFeedTrigger) => (options.Grace, options.MaxWait),
			_ => (TimeSpan.Zero, TimeSpan.Zero),
		};
}
