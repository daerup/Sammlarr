using Domain.Media;

namespace Domain.Watchlist.Outbox;

public enum WatchlistOutboxState
{
	Pending,
	Failed,
}

public enum WatchlistOperationAction
{
	Add,
	Remove,
}

public record WatchlistOutboxEntry(MediaId MediaId, WatchlistOperationAction Action, WatchlistOutboxState Status, int Attempts)
{
	public DateTimeOffset AddedAt { get; init; }
}
