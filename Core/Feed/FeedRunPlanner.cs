using Contracts.Blocklist;
using Contracts.DI;
using Contracts.Feed;
using Contracts.Processing;
using Contracts.Ratings;
using Contracts.Wantlist;
using Core.Processing;
using Domain.Feed;
using Domain.Feed.Planning;
using Domain.Media;
using Domain.Processing;
using Domain.Providers;
using Domain.Wantlist;
using Microsoft.Extensions.Logging;

namespace Core.Feed;

internal sealed class FeedRunPlanner(
	IWantlistRepository wantlist,
	IBlocklistService blocklist,
	QueryProcessor<MediaQueryRow> processor,
	IFeedRepository feeds,
	IKeyedServiceIndex<FeedProvider, IFeedProvider> providers,
	IRatingService ratings,
	ILogger<FeedRunPlanner> logger
) : IFeedRunPlanner
{
	public async Task<FeedPlanResult> PlanAsync(FeedPlanTarget target, CancellationToken cancellationToken = default)
	{
		try
		{
			return target switch
			{
				DraftFeed(var definition, var load) => await PlanAsync(definition, load, cancellationToken),
				SavedFeed(var id) => await feeds
					.FindAsync(id, cancellationToken)
					.Map(async feed => (FeedPlanResult)await PlanAsync(feed, cancellationToken))
					.GetValueOr(new FeedPlanFailed("The feed no longer exists.")),
			};
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Feed plan failed for {FeedId}", Identify(target));
			return new FeedPlanFailed(ex.Message);
		}
	}

	internal async Task<IFeedPlan> PlanAsync(Domain.Feed.Feed feed, CancellationToken cancellationToken = default)
	{
		return await PlanAsync(feed, await FetchAsync(feed, cancellationToken), cancellationToken);
	}

	private static FeedId Identify(FeedPlanTarget target) =>
		target switch
		{
			SavedFeed(var id) => id,
			DraftFeed(var definition, _) => definition.Id,
		};

	private async Task<IFeedPlan> PlanAsync(Domain.Feed.Feed feed, RawFeedLoad load, CancellationToken cancellationToken = default)
	{
		var strategy = await feed.RatingStrategy.Match(Task.FromResult, () => ratings.GetDefaultAsync(cancellationToken));
		var available = await ratings.GetAvailableAsync(cancellationToken);
		var unmatchedItems = new List<UnmatchedMedia>();
		var resolved = new List<MediaQueryRow>();
		var seen = new HashSet<MediaId>();
		foreach (var item in load.Items)
		{
			switch (item)
			{
				case MatchedMedia matched when seen.Add(matched.Identity.Id):
					resolved.Add(MediaQueryRow.From(matched, strategy, available));
					break;
				case MatchedMedia:
					break;
				case UnmatchedMedia unmatched:
					unmatchedItems.Add(unmatched);
					break;
			}
		}

		var wanted = new WantlistSnapshot(await wantlist.GetAllAsync(cancellationToken));
		var blocked = await blocklist.GetAsync(cancellationToken);
		var owned = wanted.Items.Where(item => item.Provenance is FeedOrigin origin && origin.Feed.Id == feed.Id).Select(item => item.MediaId).ToHashSet();

		var options = new QueryOptions
		{
			Sort = feed.Sort,
			Limit = feed.Limit,
			Filter = feed.Filter,
		};
		var predicates = new QueryPredicates<MediaQueryRow>(
			IsWantlisted: row => wanted.Contains(row.Media.Identity.Id),
			IsBlocklisted: row => blocked.Contains(row.Media.Identity.Id),
			IsOwned: row => owned.Contains(row.Media.Identity.Id)
		);

		var classified = resolved.Count > 0 ? await processor.ExecuteAsync(resolved, options, predicates, cancellationToken).ToListAsync(cancellationToken) : [];
		var verdicts = classified
			.Select(verdict => new QueryVerdict<MatchedMedia>(verdict.Item.Media, verdict.Outcome, verdict.Enrollment, verdict.ConsumesSlot))
			.ToList();

		var absences = feed.Strategy is Mirror mirror
			? await feeds.GetAbsencesAsync(feed.Id, owned, mirror.AbsenceThreshold, cancellationToken)
			: new Dictionary<MediaId, int>();

		return FeedPlans.Compute(new ClassifiedFeedLoad(verdicts, unmatchedItems, load.State, load.SourceFailure), owned, absences, feed.Strategy);
	}

	private async Task<RawFeedLoad> FetchAsync(Domain.Feed.Feed feed, CancellationToken cancellationToken)
	{
		var items = new List<MediaMatchResult>();
		try
		{
			await foreach (var item in providers[feed.Provider].GetFeedItemsAsync(feed.Identifier, cancellationToken))
			{
				items.Add(item);
			}
			return new RawFeedLoad(items, FeedLoadState.Finished);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogWarning(ex, "Feed {FeedId} returned a partial list; removals are suppressed for this run", feed.Id);
			return new RawFeedLoad(items, FeedLoadState.Unfinished, Some.Of(ex.Message));
		}
	}
}
