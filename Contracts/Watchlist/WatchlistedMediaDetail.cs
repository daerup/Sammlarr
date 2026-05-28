using Contracts.Media.Search;

namespace Contracts.Watchlist;

public record WatchlistedMediaDetail(IMediaDetail Detail, DateTimeOffset AddedAt, WatchlistItemProvenance Provenance);
