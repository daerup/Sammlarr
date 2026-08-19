using Domain.Identity;
using Domain.Media;

namespace Contracts.Watchlist.Outbox;

public sealed record PushWatchlistOperation(UserId UserId, MediaId MediaId);
