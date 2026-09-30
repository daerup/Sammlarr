using Contracts;
using Contracts.Feed;
using Contracts.Identity;
using Contracts.Jobs;
using Core.Announcing;
using Core.Feed;
using Domain.Feed.Run;
using Domain.Jobs;
using Domain.Wantlist.Sync;

namespace Core.Jobs;

internal sealed class FeedRunJobHandler(IFeedRunEngine engine, IJobQueue queue, Announcer announcer, ICurrentUser currentUser, IDateTimeProvider clock)
	: IJobHandler<RunFeed>
{
	public async Task HandleAsync(RunFeed request, CancellationToken cancellationToken = default)
	{
		if (request.ArmsSync)
		{
			await queue.EnqueueAsync(new SyncWantlist(new ScheduledFeedTrigger()), cancellationToken);
		}

		var result = await engine.RunAsync(request.Id, request.Trigger, cancellationToken);
		announcer.Announce(new FeedRunFinishedEvent(currentUser.Id, request.Id));
		if (FeedRunNotification.For(currentUser.Id, clock.Now, request.Id, result) is { } notification)
		{
			await announcer.AnnounceAsync(notification, cancellationToken);
		}
	}
}
