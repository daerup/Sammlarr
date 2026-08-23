using Contracts.Watchlist.Outbox;
using Domain.Watchlist.Outbox;

namespace Core.Watchlist;

internal sealed class WatchlistOutboxEventFeed : IWatchlistOutboxEventFeed
{
	public event Action<WatchlistOutboxEvent>? Published;

	public void Publish(WatchlistOutboxEvent outboxEvent) => Published?.Invoke(outboxEvent);
}
