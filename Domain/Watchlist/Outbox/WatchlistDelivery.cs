using Domain.Media;

namespace Domain.Watchlist.Outbox;

public enum WatchlistDeliveryStatus
{
	Pending,
	Failed,
}

public record WatchlistDelivery(MediaId MediaId, WatchlistOperationAction Action, WatchlistDeliveryStatus Status, int Attempts);
