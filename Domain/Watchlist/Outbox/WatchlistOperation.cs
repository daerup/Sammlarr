using Domain.Media;

namespace Domain.Watchlist.Outbox;

public enum WatchlistOperationKind
{
	Add,
	Remove,
}

public record WatchlistOperation(MediaId MediaId, WatchlistOperationKind Kind);
