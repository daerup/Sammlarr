using Domain.Media;

namespace Domain.Wantlist.Outbox;

public sealed class OutboxSnapshot(IReadOnlyList<WantlistOutboxEntry> entries)
{
	public IReadOnlyList<WantlistOutboxEntry> Entries { get; } = entries;

	public IReadOnlyList<WantlistOutboxEntry> Pending { get; } = [.. entries.Where(entry => entry.Status is WantlistOutboxState.Pending)];

	public IReadOnlyDictionary<MediaId, WantlistOutboxEntry> Adds { get; } = ByAction(entries, WantlistOperationAction.Add);

	public IReadOnlyDictionary<MediaId, WantlistOutboxEntry> Removes { get; } = ByAction(entries, WantlistOperationAction.Remove);

	private static Dictionary<MediaId, WantlistOutboxEntry> ByAction(IReadOnlyList<WantlistOutboxEntry> entries, WantlistOperationAction action) =>
		entries.Where(entry => entry.Action == action).ToDictionary(entry => entry.MediaId);
}
