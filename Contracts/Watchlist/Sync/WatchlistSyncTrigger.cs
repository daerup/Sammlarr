namespace Contracts.Watchlist.Sync;

public union WatchlistSyncTrigger(ManualTrigger);

public readonly record struct ManualTrigger
{
	public override string ToString() => "Manual";
}
