using Domain.Media;

namespace Domain.Wantlist;

public sealed class WantlistSnapshot(IReadOnlyList<WantlistItem> items)
{
	private readonly Dictionary<MediaId, WantlistItem> _items = items.ToDictionary(item => item.MediaId);

	public IReadOnlyCollection<MediaId> Ids => _items.Keys;

	public IReadOnlyCollection<WantlistItem> Items => _items.Values;

	public bool Contains(MediaId id) => _items.ContainsKey(id);

	public Optional<WantlistItem> Find(MediaId id) => _items.TryGetValue(id, out var item) ? Some.Of(item) : new None();
}
