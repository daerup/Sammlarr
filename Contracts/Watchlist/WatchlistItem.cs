namespace Contracts.Watchlist;

public record WatchlistItem(IdentifiedMedia Media, DateTimeOffset AddedAt, WatchlistItemProvenance Provenance);
