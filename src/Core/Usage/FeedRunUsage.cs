using System.Collections.Concurrent;
using Contracts.Usage;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Providers;
using Domain.Usage;

namespace Core.Usage;

internal sealed class FeedRunUsage : IFeedRunUsage
{
	private readonly ConcurrentDictionary<FeedId, Running> _running = new();

	public void Start(FeedId feed) => _running[feed] = new Running();

	public void Credit(FeedId feed, RemoteService service, UsageOutcome outcome, long calls)
	{
		_running.GetValueOrNone(feed).Apply(running => running.Calls.AddOrUpdate((service, outcome), calls, (_, seen) => seen + calls));
	}

	public void Time(FeedId feed, RemoteService service, int upToMs)
	{
		_running.GetValueOrNone(feed).Apply(running => running.Times.AddOrUpdate((service, upToMs), 1, (_, seen) => seen + 1));
	}

	public FeedRunCost End(FeedId feed)
	{
		_running.TryRemove(feed, out var running);
		return Optional.Of(running).Map(found => found.Cost()).GetValueOr(FeedRunCost.None);
	}

	private sealed class Running
	{
		internal ConcurrentDictionary<(RemoteService Service, UsageOutcome Outcome), long> Calls { get; } = new();

		internal ConcurrentDictionary<(RemoteService Service, int UpToMs), long> Times { get; } = new();

		internal FeedRunCost Cost()
		{
			return new FeedRunCost(
				[.. Calls.Select(pair => new FeedRunCalls(pair.Key.Service, pair.Key.Outcome, pair.Value))],
				[.. Times.Select(pair => new FeedRunTimes(pair.Key.Service, pair.Key.UpToMs, pair.Value))]
			);
		}
	}
}
