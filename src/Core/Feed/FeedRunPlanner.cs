using Contracts.Blocklist;
using Contracts.DI;
using Contracts.Feed;
using Contracts.Identity;
using Contracts.Processing;
using Contracts.Ratings;
using Contracts.Wantlist;
using Core.Processing;
using Domain.Blocklist;
using Domain.Feed;
using Domain.Feed.Planning;
using Domain.Media;
using Domain.Processing;
using Domain.Providers;
using Domain.Wantlist;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Feed;

internal sealed class FeedRunPlanner(
	IWantlistRepository wantlist,
	IBlocklistService blocklist,
	QueryProcessor<MediaQueryRow> processor,
	IFeedRepository feeds,
	IServiceScopeFactory scopes,
	ICurrentUser currentUser,
	IRatingService ratings,
	TimeProvider clock,
	ILogger<FeedRunPlanner> logger
) : IFeedRunPlanner
{
	public async Task<FeedPlanResult> PlanAsync(FeedPlanTarget target, CancellationToken cancellationToken = default)
	{
		try
		{
			return target switch
			{
				DraftFeed(var definition, var load) => await PlanAsync(definition, load, await RatingAsync(definition, cancellationToken), cancellationToken),
				SavedFeed(var id) => await feeds
					.FindAsync(id, cancellationToken)
					.Map(async feed => (FeedPlanResult)await PlanAsync(feed, cancellationToken))
					.GetValueOr(new FeedPlanFailed("The feed no longer exists.")),
			};
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Feed plan failed for {FeedId}", Identify(target));
			return new FeedPlanFailed(ex.Message);
		}
	}

	public async Task<FieldNeeds> NeedsAsync(Domain.Feed.Feed feed, CancellationToken cancellationToken = default)
	{
		var rating = await RatingAsync(feed, cancellationToken);
		return feed.Needs(rating.Strategy, rating.Available);
	}

	internal async Task<IFeedPlan> PlanAsync(Domain.Feed.Feed feed, CancellationToken cancellationToken = default)
	{
		var rating = await RatingAsync(feed, cancellationToken);
		var load = await FetchAsync(feed, feed.Needs(rating.Strategy, rating.Available), cancellationToken);
		return await PlanAsync(feed, load, rating, cancellationToken);
	}

	private async Task<FeedRating> RatingAsync(Domain.Feed.Feed feed, CancellationToken cancellationToken)
	{
		var strategy = await feed.RatingStrategy.GetValueOr(() => ratings.GetDefaultAsync(cancellationToken));
		return new FeedRating(strategy, await ratings.GetAvailableAsync(cancellationToken));
	}

	private static FeedId Identify(FeedPlanTarget target) =>
		target switch
		{
			SavedFeed(var id) => id,
			DraftFeed(var definition, _) => definition.Id,
		};

	private async Task<IFeedPlan> PlanAsync(Domain.Feed.Feed feed, RawFeedLoad load, FeedRating rating, CancellationToken cancellationToken)
	{
		var (resolved, unmatchedItems) = Split(load, rating);
		var wanted = new WantlistSnapshot(await wantlist.GetAllAsync(cancellationToken));
		var blocked = await blocklist.GetAsync(cancellationToken);
		var owned = wanted.Items.Where(item => item.Provenance is FeedOrigin origin && origin.Feed.Id == feed.Id).Select(item => item.MediaId).ToHashSet();

		var verdicts = await ClassifyAsync(feed, resolved, wanted, blocked, owned, cancellationToken);

		var absences = feed.Strategy is Mirror ? await feeds.GetAbsencesAsync(feed.Id, cancellationToken) : new Dictionary<MediaId, int>();

		return FeedPlans.Compute(new ClassifiedFeedLoad(verdicts, unmatchedItems, load.State, load.SourceFailure), owned, absences, feed.Strategy);
	}

	private static (List<MediaQueryRow> Resolved, List<UnmatchedMedia> Unmatched) Split(RawFeedLoad load, FeedRating rating)
	{
		var (strategy, available) = rating;
		var unmatchedItems = new List<UnmatchedMedia>();
		var resolved = new List<MediaQueryRow>();
		var seen = new HashSet<MediaId>();
		foreach (var item in load.Items.OrderBy(listed => listed.Position).Select(listed => listed.Item))
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

		return (resolved, unmatchedItems);
	}

	private async Task<List<QueryVerdict<MatchedMedia>>> ClassifyAsync(
		Domain.Feed.Feed feed,
		List<MediaQueryRow> resolved,
		WantlistSnapshot wanted,
		BlocklistSnapshot blocked,
		HashSet<MediaId> owned,
		CancellationToken cancellationToken
	)
	{
		var options = new QueryOptions
		{
			Sort = feed.Sort,
			Limit = feed.Limit,
			Filter = feed.Filter,
			ShuffleSeed = await ShuffleSeedAsync(feed, cancellationToken),
			DrawSkipsOwned = feed.Strategy is Append,
			Today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime),
		};
		var predicates = new QueryPredicates<MediaQueryRow>(
			IsWantlisted: row => wanted.Contains(row.Media.Identity.Id),
			IsBlocklisted: row => blocked.Contains(row.Media.Identity.Id),
			IsOwned: row => owned.Contains(row.Media.Identity.Id),
			Identity: row => row.Media.Identity.Id.Value
		);

		var classified = resolved.Count > 0 ? await processor.ExecuteAsync(resolved, options, predicates, cancellationToken).ToListAsync(cancellationToken) : [];
		return [.. classified.Select(verdict => new QueryVerdict<MatchedMedia>(verdict.Item.Media, verdict.Outcome, verdict.Enrollment, verdict.ConsumesSlot))];
	}

	private async Task<Guid> ShuffleSeedAsync(Domain.Feed.Feed feed, CancellationToken cancellationToken)
	{
		if (!feed.Limit.Match(limit => limit.Pick is LimitPick.Random, () => false))
		{
			return feed.Id.Value;
		}

		var lastRun = await feeds.LastConclusiveRunStartAsync(feed.Id, cancellationToken);
		return lastRun.Map(started => ShuffleRank.Reseed(feed.Id.Value, started)).GetValueOr(feed.Id.Value);
	}

	private async Task<RawFeedLoad> FetchAsync(Domain.Feed.Feed feed, FieldNeeds needs, CancellationToken cancellationToken)
	{
		var items = new List<Listed<MediaMatchResult>>();
		await using var scope = scopes.CreateAsyncScope();
		using var user = scope.ServiceProvider.GetRequiredService<IUserScope>().As(currentUser);
		try
		{
			var provider = scope.ServiceProvider.GetRequiredService<IKeyedServiceIndex<FeedProvider, IFeedProvider>>()[feed.Provider];
			await foreach (var item in provider.GetFeedItemsAsync(feed.Identifier, needs, cancellationToken))
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

	private readonly record struct FeedRating(RatingStrategy Strategy, ValueSet<RatingSource> Available);
}
