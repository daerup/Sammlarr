using Domain.Watchlist.Outbox;

namespace Contracts.Watchlist.Outbox;

public interface IWatchlistOutboxEventFeed
{
	event Action<WatchlistOutboxEvent>? Published;

	void Publish(WatchlistOutboxEvent outboxEvent);
}
