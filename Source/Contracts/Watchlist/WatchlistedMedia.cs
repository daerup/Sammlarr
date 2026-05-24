using Contracts.Media;

namespace Contracts.Watchlist;

public record WatchlistedMedia(MediaId MediaId, DateTimeOffset AddedAt, WatchlistItemProvenance Provenance);
