using Contracts;
using Contracts.Feed;
using Contracts.Identity;
using Contracts.Jobs;
using Core.Announcing;
using Core.Feed;
using Domain.Feed.Run;
using Domain.Jobs;
using Domain.Wantlist.Sync;
using Observability.Usage;

namespace Core.Jobs;

internal sealed class FeedRunJobHandler(IFeedRunEngine engine, IJobQueue queue, Announcer announcer, ICurrentUser currentUser, IDateTimeProvider clock)
	: IJobHandler<RunFeed>
{
	public async Task HandleAsync(RunFeed request, CancellationToken cancellationToken = default)
	{
		using var marked = UsageCauses.Mark(request.Id);
		if (request.ArmsSync)
		{
			await queue.EnqueueAsync(new SyncWantlist(new ScheduledFeedTrigger()), cancellationToken);
		}

		var result = await engine.RunAsync(request.Id, request.Trigger, cancellationToken);
		await queue.EnqueueAsync(new FillDetails(), cancellationToken);
		announcer.Announce(new FeedRunFinishedEvent(currentUser.Id, request.Id));
		await FeedRunNotification
			.For(currentUser.Id, clock.Now, request.Id, result)
			.Apply(notification => announcer.AnnounceAsync(notification, cancellationToken));
	}
}
