using Domain.Announcing;
using Domain.Identity;
using Domain.Media;

namespace Domain.Watchlist.Outbox;

public enum WatchlistOutboxEventKind
{
	Pushed,
	Retrying,
	Abandoned,
}

public record WatchlistOutboxEvent(UserId UserId, MediaId MediaId, WatchlistOutboxEventKind Kind) : IEvent;
