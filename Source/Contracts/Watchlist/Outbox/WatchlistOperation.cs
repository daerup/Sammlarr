using Contracts.Media;

namespace Contracts.Watchlist.Outbox;

public enum WatchlistOperationKind
{
	Add,
	Remove,
}

public record WatchlistOperation(MediaId MediaId, WatchlistOperationKind Kind);
