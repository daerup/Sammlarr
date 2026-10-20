using Contracts.Jobs;
using Domain.Feed;
using Domain.Jobs;
using Domain.Wantlist.Sync;
using Humanizer;

namespace UI.Features.Feeds.Components;

internal static class FeedRunQueue
{
	internal static async Task<string> EnqueueAsync(IJobQueue queue, IReadOnlyList<FeedId> feeds, bool syncAfter, CancellationToken cancellationToken)
	{
		var queued = 0;
		foreach (var feedId in feeds)
		{
			if (await queue.EnqueueAsync(new RunFeed(feedId, new Manual(), syncAfter), cancellationToken) is Queued)
			{
				queued++;
			}
		}

		if (syncAfter && queued > 0)
		{
			await queue.EnqueueAsync(new SyncWantlist(new ScheduledFeedTrigger()), cancellationToken);
		}

		return Summary(queued, feeds.Count - queued);
	}

	private static string Summary(int queued, int alreadyQueued) =>
		(queued, alreadyQueued) switch
		{
			(0, _) => $"{FeedCount(alreadyQueued)} already queued.",
			(_, 0) => $"Queued {FeedCount(queued)}. They run one at a time.",
			_ => $"Queued {FeedCount(queued)}, {alreadyQueued} already queued. They run one at a time.",
		};

	private static string FeedCount(int count) => "feed".ToQuantity(count);
}
