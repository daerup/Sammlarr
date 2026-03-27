using Contracts.Media;

namespace Contracts.Watchlist;

public sealed record WatchlistPageResult(IReadOnlyList<IWatchlistItem> Items, int TotalPages);
