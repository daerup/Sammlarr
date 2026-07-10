using Domain.Media;

namespace Domain.Watchlist;

public record WatchlistedMedia(IdentifiedMedia Media, DateTimeOffset AddedAt, WatchlistItemProvenance Provenance);
