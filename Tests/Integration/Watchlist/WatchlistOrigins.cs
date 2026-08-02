using DB.Data.Watchlist;
using Domain.Providers;
using Domain.Watchlist;

namespace Integration.Tests.Watchlist;

internal static class WatchlistOrigins
{
	public static WatchlistEntryOrigin Parse(string origin) => Enum.Parse<WatchlistEntryOrigin>(origin, ignoreCase: true);

	public static WatchlistItemProvenance ToProvenance(WatchlistEntryOrigin origin) =>
		origin switch
		{
			WatchlistEntryOrigin.Manual => new WatchlistItemProvenance(new ManualOrigin(ManualSource.Unknown)),
			WatchlistEntryOrigin.Remote => new WatchlistItemProvenance(new RemoteOrigin(WatchlistProvider.Plex)),
			_ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null),
		};
}
