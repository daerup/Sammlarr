using Contracts.Media.Search;

namespace Contracts.Watchlist;

public record WatchlistedItem(IMediaDetailResult Detail, DateTimeOffset AddedAt, WatchlistItemProvenance Provenance);
