using Domain.Media;

namespace Domain.Watchlist.Outbox;

public enum WatchlistOperationAction
{
	Add,
	Remove,
}

public record WatchlistOperation(MediaId MediaId, WatchlistOperationAction Action);
