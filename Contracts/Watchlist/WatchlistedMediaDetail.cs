namespace Contracts.Watchlist;

public record WatchlistedMediaDetail(MediaLookup Detail, DateTimeOffset AddedAt, WatchlistItemProvenance Provenance);
