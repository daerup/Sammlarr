using Contracts.Media;
using Contracts.Watchlist.Outbox;

namespace Contracts.Watchlist.Sync;

public union WatchlistSyncDryRun(WatchlistSyncDryRunReport report, WatchlistSyncDryRunRemoteFailed remoteFailed);

public record WatchlistSyncDryRunReport(IReadOnlyList<WatchlistSyncChange> Changes, int InSync)
{
	public int AddCount => Changes.Count(c => c.Kind == WatchlistOperationKind.Add);
	public int RemoveCount => Changes.Count(c => c.Kind == WatchlistOperationKind.Remove);
	public int TotalChanges => Changes.Count;
}

public record WatchlistSyncChange(IMediaId MediaId, WatchlistSyncDirection Direction, WatchlistOperationKind Kind);

public enum WatchlistSyncDirection
{
	Pull,
	Push,
}

public readonly record struct WatchlistSyncDryRunRemoteFailed(string Reason);
