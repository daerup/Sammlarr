using Contracts;
using Contracts.Identity;
using Contracts.Jobs;
using Core.Announcing;
using Domain.Jobs;
using Domain.Wantlist.Sync;
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
	private static readonly SyncWantlist ScheduledSync = new(new ScheduledFeedTrigger());

	public async Task<EnqueueResult> EnqueueAsync(IJobRequest request, CancellationToken cancellationToken = default)
	{
		var now = clock.Now;
		var (delay, maxWait, skipWhileRunning) = Arming(request);
		var runAfter = now + delay;
		var result = await jobs.EnqueueAsync(new QueuedJob(request, currentUser.Id, runAfter, maxWait, skipWhileRunning), now, cancellationToken);
		var syncQueuedBehindFeeds = await QueueSyncBehindFeedsAsync(request, now, cancellationToken);
		if (syncQueuedBehindFeeds && request is SyncWantlist)
		{
			result = AsQueued(result);
		}

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
		if (syncQueuedBehindFeeds && request is RunFeed)
		{
			announcer.Announce(new JobStateChangedEvent(currentUser.Id, ScheduledSync.Kind, ScheduledSync.Key, JobState.Queued));
		}

		return result;
	}

	public async Task<bool> CancelAsync(JobId id, CancellationToken cancellationToken = default)
	{
		if (await jobs.CancelAsync(id, currentUser.Id, clock.Now, cancellationToken) is not Some<IJobRequest>(var request))
		{
			return false;
		}

		await alarm.ArmAsync(cancellationToken);
		announcer.Announce(new JobStateChangedEvent(currentUser.Id, request.Kind, request.Key, JobState.Cancelled));
		return true;
	}

	private async Task<bool> QueueSyncBehindFeedsAsync(IJobRequest request, DateTimeOffset now, CancellationToken cancellationToken)
	{
		if (request is not (RunFeed { ArmsSync: true } or SyncWantlist(ScheduledFeedTrigger)))
		{
			return false;
		}

		var queuedRuns = await jobs.QueuedAsync(currentUser.Id, JobKind.FeedRun, cancellationToken);
		if (!queuedRuns.Any(run => run is RunFeed { ArmsSync: true }))
		{
			return false;
		}

		var queued = await jobs.QueueWaitingAsync(currentUser.Id, ScheduledSync.Kind, now, cancellationToken);
		return queued > 0;
	}

	private static EnqueueResult AsQueued(EnqueueResult result) =>
		result switch
		{
			Queued queued => queued with { State = JobState.Queued },
			AlreadyQueued folded => folded with { State = JobState.Queued },
		};

	private (TimeSpan Delay, TimeSpan MaxWait, bool SkipWhileRunning) Arming(IJobRequest request) =>
		request switch
		{
			SyncWantlist(ScheduledFeedTrigger) => (options.Grace, options.MaxWait, false),
			RunFeed => (TimeSpan.Zero, TimeSpan.Zero, true),
			_ => (TimeSpan.Zero, TimeSpan.Zero, false),
		};
}
