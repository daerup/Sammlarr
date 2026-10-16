namespace UI.Shared.Media;

public sealed class BulkSelection<TKey>
	where TKey : notnull
{
	private readonly HashSet<TKey> _selected = [];
	private (TKey Key, bool Selected)? _anchor;

	public int Count => _selected.Count;

	public IReadOnlySet<TKey> Keys => _selected;

	public bool Contains(TKey key) => _selected.Contains(key);

	public void Click(TKey key, bool extend, IReadOnlyList<TKey> page)
	{
		if (extend && _anchor is { } anchor && IndexOf(page, anchor.Key) is { } from && IndexOf(page, key) is { } to)
		{
			var (low, high) = from <= to ? (from, to) : (to, from);
			foreach (var ranged in page.Skip(low).Take(high - low + 1))
			{
				if (anchor.Selected)
				{
					_selected.Add(ranged);
				}
				else
				{
					_selected.Remove(ranged);
				}
			}

			return;
		}

		var selected = _selected.Add(key);
		if (!selected)
		{
			_selected.Remove(key);
		}

		_anchor = (key, selected);
	}

	public void SelectAll(IEnumerable<TKey> keys)
	{
		_selected.Clear();
		_selected.UnionWith(keys);
	}

	public void Clear()
	{
		_selected.Clear();
		_anchor = null;
	}

	private static int? IndexOf(IReadOnlyList<TKey> page, TKey key)
	{
		for (var index = 0; index < page.Count; index++)
		{
			if (EqualityComparer<TKey>.Default.Equals(page[index], key))
			{
				return index;
			}
		}

		return null;
	}
}
