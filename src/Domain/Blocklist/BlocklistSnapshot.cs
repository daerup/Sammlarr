using Domain.Media;

namespace Domain.Blocklist;

public sealed class BlocklistSnapshot(IReadOnlyList<BlocklistItem> items)
{
	private readonly HashSet<MediaId> _ids = [.. items.Select(item => item.MediaId)];

	public IReadOnlyCollection<MediaId> Ids => _ids;

	public IReadOnlyCollection<BlocklistItem> Items { get; } = items;

	public bool Contains(MediaId id) => _ids.Contains(id);
}
