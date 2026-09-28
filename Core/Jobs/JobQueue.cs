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
	JobAlarm alarm,
	ICurrentUser currentUser,
	IDateTimeProvider clock,
	SchedulerOptions options,
	Announcer announcer
) : IJobQueue
{
	public async Task<EnqueueResult> EnqueueAsync(IJobRequest request, CancellationToken cancellationToken = default)
	{
		var now = clock.Now;
		var (delay, maxWait, skipWhileRunning) = Arming(request);
		var runAfter = now + delay;
		var result = await jobs.EnqueueAsync(new QueuedJob(request, currentUser.Id, runAfter, maxWait, skipWhileRunning), now, cancellationToken);
		var state = result switch
		{
			Queued queued => queued.State,
			AlreadyQueued folded => folded.State,
		};
		if (state is JobState.Queued)
		{
			signal.Raise();
		}

		await alarm.ArmAsync(cancellationToken);
		announcer.Announce(new JobStateChangedEvent(currentUser.Id, request.Kind, request.Key, state));
		return result;
	}

	private (TimeSpan Delay, TimeSpan MaxWait, bool SkipWhileRunning) Arming(IJobRequest request) =>
		request switch
		{
			SyncWatchlist(ScheduledFeedTrigger) => (options.Grace, options.MaxWait, false),
			RunFeed => (TimeSpan.Zero, TimeSpan.Zero, true),
			_ => (TimeSpan.Zero, TimeSpan.Zero, false),
		};
}
