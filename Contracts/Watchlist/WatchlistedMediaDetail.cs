namespace Contracts.Watchlist;

public record WatchlistedMediaDetail(MediaMatch Detail, DateTimeOffset AddedAt, WatchlistItemProvenance Provenance);
