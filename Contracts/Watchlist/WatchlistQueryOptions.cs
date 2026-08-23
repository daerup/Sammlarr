namespace Contracts.Watchlist;

public sealed record WatchlistQueryOptions(bool IncludeRemoved = false)
{
	public static WatchlistQueryOptions Default { get; } = new WatchlistQueryOptions();

	public static WatchlistQueryOptions WithRemoved { get; } = new WatchlistQueryOptions(IncludeRemoved: true);
}
