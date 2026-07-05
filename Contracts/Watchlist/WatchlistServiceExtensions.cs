namespace Contracts.Watchlist;

public static class WatchlistServiceExtensions
{
	extension(IWatchlistService)
	{
		public static int RecommendedConcurrency => 10;
	}
}
