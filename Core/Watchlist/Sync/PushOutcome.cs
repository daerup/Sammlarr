using Domain.Media;
using Domain.Watchlist.Outbox;

namespace Core.Watchlist.Sync;

internal readonly record struct PushOutcome(MediaId MediaId, WatchlistOperationAction Action, WatchlistOutboxEventKind Kind);
