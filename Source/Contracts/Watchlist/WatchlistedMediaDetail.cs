using Contracts.Media.Search;

namespace Contracts.Watchlist;

public record WatchlistedMediaDetail(IMediaDetailResult Detail, DateTimeOffset AddedAt, WatchlistItemProvenance Provenance);
