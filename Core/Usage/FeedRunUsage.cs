using System.Collections.Concurrent;
using Contracts.Usage;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Providers;
using Domain.Usage;

namespace Core.Usage;

internal sealed class FeedRunUsage : IFeedRunUsage
{
	private readonly ConcurrentDictionary<FeedId, ConcurrentDictionary<(RemoteService Service, UsageOutcome Outcome), long>> _running = new();

	public void Start(FeedId feed) => _running[feed] = new();

	public void Credit(FeedId feed, RemoteService service, UsageOutcome outcome, long calls)
	{
		_running.GetValueOrNone(feed).Apply(counts => counts.AddOrUpdate((service, outcome), calls, (_, seen) => seen + calls));
	}

	public IReadOnlyList<FeedRunCalls> End(FeedId feed)
	{
		_running.TryRemove(feed, out var counts);
		return [.. Optional.Of(counts).Map(found => found.Select(pair => new FeedRunCalls(pair.Key.Service, pair.Key.Outcome, pair.Value))).GetValueOr([])];
	}
}
