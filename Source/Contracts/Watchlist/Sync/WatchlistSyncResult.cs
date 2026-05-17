namespace Contracts.Watchlist.Sync;

public union WatchlistSyncResult(
	WatchlistSyncSuccess success,
	WatchlistSyncUnchanged unchanged,
	WatchlistSyncAlreadyRunning alreadyRunning,
	WatchlistSyncRemoteFailed remoteFailed
);

public readonly record struct WatchlistSyncSuccess(int Inserted, int Confirmed, int HardDeleted, int PushedAdds, int PushedRemoves, int RemoteFailures)
{
	public int TotalAdded => Inserted + PushedAdds;
	public int TotalRemoved => HardDeleted + PushedRemoves;

	public override string ToString()
	{
		return (TotalAdded, TotalRemoved) switch
		{
			(0, 0) => "Synced: no changes.",
			(> 0, 0) => $"Added: {TotalAdded}",
			(0, > 0) => $"Removed: {TotalRemoved}",
			_ => $"Synced: +{TotalAdded} added, -{TotalRemoved} removed.",
		};
	}
}

public readonly record struct WatchlistSyncUnchanged
{
	public override string ToString() => "Synced: no changes.";
}

public readonly record struct WatchlistSyncAlreadyRunning
{
	public override string ToString() => "Sync already in progress.";
}

public readonly record struct WatchlistSyncRemoteFailed(string Reason)
{
	public override string ToString() => $"Couldn't reach the provider - {Reason}";
}
