using Contracts.Media;

namespace Contracts.Watchlist;

public record WatchlistEntry(IMediaId Id, DateTimeOffset AddedAt, WatchlistItemProvenance Provenance);
