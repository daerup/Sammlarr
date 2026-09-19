using Contracts.Blacklist;
using Contracts.Feed;
using Contracts.Processing;
using Contracts.Watchlist;
using Core.Processing;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Processing;
using Domain.Watchlist;

namespace Core.Feed;

internal sealed class FeedRunEngine(IWatchlistRepository watchlist, IBlacklistService blacklist, QueryProcessor<MediaQueryRow> processor) : IFeedRunEngine
{
	public async Task<FeedRunSummary> DryRunAsync(
		Domain.Feed.Feed feed,
		IReadOnlyList<MediaLookup> items,
		FeedSource source,
		CancellationToken cancellationToken = default
	)
	{
		var unresolved = items.Where(item => item is UnmatchedMedia).ToList();
		var resolved = items.Where(item => item is not UnmatchedMedia).Select(MediaQueryRow.From).ToList();

		var entries = await watchlist.GetEntriesAsync(cancellationToken);
		var blacklisted = await blacklist.GetBlacklistIdsAsync(cancellationToken);
		var watchlisted = entries.Select(entry => entry.MediaId).ToHashSet();
		var owned = entries.Where(entry => entry.Provenance is FeedOrigin origin && origin.FeedId == feed.Id.Value).Select(entry => entry.MediaId).ToHashSet();

		var options = new QueryOptions
		{
			Sort = feed.Sort,
			Limit = feed.Limit,
			Filter = feed.Filter,
		};
		var predicates = new QueryPredicates<MediaQueryRow>(
			IsWatchlisted: row => watchlisted.Contains(row.Media.Identity.Id),
			IsBlacklisted: row => blacklisted.Contains(row.Media.Identity.Id),
			IsOwned: row => owned.Contains(row.Media.Identity.Id)
		);

		var classified = resolved.Count > 0 ? await processor.ExecuteAsync(resolved, options, predicates, cancellationToken).ToListAsync(cancellationToken) : [];
		var verdicts = classified
			.Select(verdict => new QueryVerdict<MediaLookup>(verdict.Item.Media, verdict.Outcome, verdict.Enrollment, verdict.ConsumesSlot))
			.ToList();

		var plan = FeedRunPlan.Compute(verdicts, unresolved.Count, owned, feed.Strategy, source);
		return new FeedRunSummary(verdicts, unresolved, plan.Delta, plan.Conclusiveness);
	}
}
