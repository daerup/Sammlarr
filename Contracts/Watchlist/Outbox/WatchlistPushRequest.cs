using Domain.Identity;
using Domain.Media;

namespace Contracts.Watchlist.Outbox;

public sealed record WatchlistPushRequest(UserId UserId, MediaId MediaId);
