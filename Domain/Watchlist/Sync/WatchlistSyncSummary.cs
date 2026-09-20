using Domain.Media;
using Domain.Watchlist.Outbox;

namespace Domain.Watchlist.Sync;

public record WatchlistSyncSummary(WatchlistDelta Pull, WatchlistDelta Push, IReadOnlyList<MediaId> NoOps)
{
	public IReadOnlyList<UnmatchedMedia> Unmatched { get; init; } = [];

	private int Pulled => Pull.Adds.Count;
	private int Removed => Pull.Removes.Count;
	private int Queued => Push.Count;
	public int AddCount => Pull.Adds.Count + Push.Adds.Count;
	public int RemoveCount => Pull.Removes.Count + Push.Removes.Count;
	public int TotalChanges => Pull.Count + Push.Count;

	public IEnumerable<WatchlistSyncChange> Changes =>
		Pull
			.Adds.Select(id => new WatchlistSyncChange(id, WatchlistSyncDirection.Pull, WatchlistOperationAction.Add))
			.Concat(Pull.Removes.Select(id => new WatchlistSyncChange(id, WatchlistSyncDirection.Pull, WatchlistOperationAction.Remove)))
			.Concat(Push.Adds.Select(id => new WatchlistSyncChange(id, WatchlistSyncDirection.Push, WatchlistOperationAction.Add)))
			.Concat(Push.Removes.Select(id => new WatchlistSyncChange(id, WatchlistSyncDirection.Push, WatchlistOperationAction.Remove)));

	public override string ToString()
	{
		var parts = new List<string>(3);
		if (Pulled > 0)
		{
			parts.Add($"pulled {Pulled}");
		}
		if (Removed > 0)
		{
			parts.Add($"removed {Removed}");
		}
		if (Queued > 0)
		{
			parts.Add($"queued {Queued} for push");
		}
		return parts.Count == 0 ? "Synced: no changes." : $"Synced: {string.Join(", ", parts)}.";
	}
}

public record WatchlistSyncChange(MediaId MediaId, WatchlistSyncDirection Direction, WatchlistOperationAction Action);

public enum WatchlistSyncDirection
{
	Pull,
	Push,
}
