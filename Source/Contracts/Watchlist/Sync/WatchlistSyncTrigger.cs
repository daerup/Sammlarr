namespace Contracts.Watchlist.Sync;

public union WatchlistSyncTrigger(ManualTrigger manual);

public readonly record struct ManualTrigger
{
	public override string ToString() => "Manual";
}
