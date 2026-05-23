using Contracts.Media;

namespace Contracts.Watchlist;

public record WatchlistedMedia(IMediaId MediaId, DateTimeOffset AddedAt, WatchlistItemProvenance Provenance);
