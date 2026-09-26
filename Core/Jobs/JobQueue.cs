using Contracts;
using Contracts.Identity;
using Contracts.Jobs;
using Domain.Jobs;
using Shared.Options;

namespace Core.Jobs;

internal sealed class JobQueue(IJobRepository jobs, JobSignal signal, ICurrentUser currentUser, IDateTimeProvider clock, SchedulerOptions options) : IJobQueue
{
	public async Task<EnqueueResult> EnqueueAsync(IJobRequest request, CancellationToken cancellationToken = default)
	{
		var (delay, maxWait) = Arming(request.Kind);
		var result = await jobs.EnqueueAsync(new QueuedJob(request, currentUser.Id, clock.Now + delay, maxWait), cancellationToken);
		signal.Raise();
		return result;
	}

	private (TimeSpan Delay, TimeSpan MaxWait) Arming(JobKind kind) =>
		kind switch
		{
			JobKind.FeedRun => (TimeSpan.Zero, TimeSpan.Zero),
			JobKind.WatchlistSync => (options.Grace, options.MaxWait),
		};
}
