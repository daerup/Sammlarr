using Contracts.Media.Search;

namespace Contracts.Watchlist;

public record WatchlistPageResult(IReadOnlyList<IMediaDetailResult> Items, int TotalPages);
