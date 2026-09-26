using Contracts.Feed;
using Contracts.Jobs;
using Domain.Feed;
using Domain.Jobs;
using Domain.Watchlist.Sync;

namespace Core.Jobs;

internal sealed class FeedRunJobHandler(IFeedRunEngine engine, IJobQueue queue) : IJobHandler<RunFeed>
{
	public async Task HandleAsync(RunFeed request, CancellationToken cancellationToken = default)
	{
		await engine.RunAsync(request.Id, request.Trigger, cancellationToken);
		if (request.Trigger is Scheduled)
		{
			await queue.EnqueueAsync(new SyncWatchlist(new ScheduledFeedTrigger()), cancellationToken);
		}
	}
}
