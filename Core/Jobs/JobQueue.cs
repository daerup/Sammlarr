using Contracts;
using Contracts.Identity;
using Contracts.Jobs;
using Domain.Jobs;
using Domain.Watchlist.Sync;
using Shared.Options;

namespace Core.Jobs;

internal sealed class JobQueue(IJobRepository jobs, JobSignal signal, ICurrentUser currentUser, IDateTimeProvider clock, SchedulerOptions options) : IJobQueue
{
	public async Task<EnqueueResult> EnqueueAsync(IJobRequest request, CancellationToken cancellationToken = default)
	{
		var (delay, maxWait) = Arming(request);
		var result = await jobs.EnqueueAsync(new QueuedJob(request, currentUser.Id, clock.Now + delay, maxWait), cancellationToken);
		signal.Raise();
		return result;
	}

	private (TimeSpan Delay, TimeSpan MaxWait) Arming(IJobRequest request) =>
		request switch
		{
			SyncWatchlist(ScheduledFeedTrigger) => (options.Grace, options.MaxWait),
			_ => (TimeSpan.Zero, TimeSpan.Zero),
		};
}
