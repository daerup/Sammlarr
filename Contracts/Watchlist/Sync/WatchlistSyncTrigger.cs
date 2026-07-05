namespace Contracts.Watchlist.Sync;

public union WatchlistSyncTrigger(ManualTrigger)
{
	public override string ToString() => this switch{
        ManualTrigger mt=> mt.ToString(),
    };
}

public readonly record struct ManualTrigger
{
	public override string ToString() => "Manual";
}
