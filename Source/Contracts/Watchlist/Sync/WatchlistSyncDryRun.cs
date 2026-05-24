using Contracts.Media;
using Contracts.Watchlist.Outbox;

namespace Contracts.Watchlist.Sync;

public record WatchlistSyncDryRun(IReadOnlyList<WatchlistSyncChange> Changes, int InSync)
{
	public int AddCount => Changes.Count(c => c.Kind == WatchlistOperationKind.Add);
	public int RemoveCount => Changes.Count(c => c.Kind == WatchlistOperationKind.Remove);
	public int TotalChanges => Changes.Count;
}

public record WatchlistSyncChange(MediaId MediaId, WatchlistSyncDirection Direction, WatchlistOperationKind Kind);

public enum WatchlistSyncDirection
{
	Pull,
	Push,
}
