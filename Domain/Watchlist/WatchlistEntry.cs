using Domain.Media;

namespace Domain.Watchlist;

public record WatchlistEntry(MediaId MediaId, DateTimeOffset AddedAt, WatchlistItemProvenance Provenance);
