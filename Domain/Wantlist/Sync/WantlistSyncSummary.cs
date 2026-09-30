using Domain.Media;
using Domain.Wantlist.Outbox;

namespace Domain.Wantlist.Sync;

public record WantlistSyncSummary(WantlistDelta Pull, WantlistDelta Push, IReadOnlyList<MediaId> NoOps)
{
	public IReadOnlyList<UnmatchedMedia> Unmatched { get; init; } = [];

	private int Pulled => Pull.Adds.Count;
	private int Removed => Pull.Removes.Count;
	private int Queued => Push.Count;
	public int AddCount => Pull.Adds.Count + Push.Adds.Count;
	public int RemoveCount => Pull.Removes.Count + Push.Removes.Count;
	public int TotalChanges => Pull.Count + Push.Count;

	public IEnumerable<WantlistSyncChange> Changes =>
		Pull
			.Adds.Select(id => new WantlistSyncChange(id, WantlistSyncDirection.Pull, WantlistOperationAction.Add))
			.Concat(Pull.Removes.Select(id => new WantlistSyncChange(id, WantlistSyncDirection.Pull, WantlistOperationAction.Remove)))
			.Concat(Push.Adds.Select(id => new WantlistSyncChange(id, WantlistSyncDirection.Push, WantlistOperationAction.Add)))
			.Concat(Push.Removes.Select(id => new WantlistSyncChange(id, WantlistSyncDirection.Push, WantlistOperationAction.Remove)));

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
			parts.Add($"pushed {Queued}");
		}
		return parts.Count == 0 ? "Synced: no changes." : $"Synced: {string.Join(", ", parts)}.";
	}
}

public record WantlistSyncChange(MediaId MediaId, WantlistSyncDirection Direction, WantlistOperationAction Action);

public enum WantlistSyncDirection
{
	Pull,
	Push,
}
