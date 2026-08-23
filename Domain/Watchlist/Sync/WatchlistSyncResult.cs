namespace Domain.Watchlist.Sync;

public union WatchlistSyncResult(WatchlistSyncSummary, WatchlistSyncAlreadyRunning, WatchlistSyncRemoteFailed);

public readonly record struct WatchlistSyncAlreadyRunning
{
	public override string ToString() => "Sync already in progress.";
}

public readonly record struct WatchlistSyncRemoteFailed(string Reason)
{
	public override string ToString() => $"Couldn't reach the provider - {Reason}";
}
