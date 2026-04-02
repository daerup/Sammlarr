namespace Contracts.Watchlist;

public record WatchlistPageResult(IReadOnlyList<IWatchlistItem> Items, int TotalPages);
