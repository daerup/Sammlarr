using System.Collections;
using System.Runtime.CompilerServices;

namespace Domain;

[CollectionBuilder(typeof(ValueSet), nameof(ValueSet.Create))]
public readonly struct ValueSet<T> : IReadOnlySet<T>, IEquatable<ValueSet<T>>
{
	private static readonly HashSet<T> Empty = [];

	internal ValueSet(HashSet<T> items) => Items = items;

	private HashSet<T> Items => field ?? Empty;

	public int Count => Items.Count;

	public bool Contains(T item) => Items.Contains(item);

	public static ValueSet<T> operator +(ValueSet<T> left, IEnumerable<T> right) => [.. left, .. right];

	public bool IsSubsetOf(IEnumerable<T> other) => Items.IsSubsetOf(other);

	public bool IsSupersetOf(IEnumerable<T> other) => Items.IsSupersetOf(other);

	public bool IsProperSubsetOf(IEnumerable<T> other) => Items.IsProperSubsetOf(other);

	public bool IsProperSupersetOf(IEnumerable<T> other) => Items.IsProperSupersetOf(other);

	public bool Overlaps(IEnumerable<T> other) => Items.Overlaps(other);

	public bool SetEquals(IEnumerable<T> other) => Items.SetEquals(other);

	public bool Equals(ValueSet<T> other) => Items.SetEquals(other.Items);

	public override bool Equals(object? obj) => obj is ValueSet<T> other && Equals(other);

	public override int GetHashCode()
	{
		var hash = 0;
		foreach (var item in Items)
		{
			hash ^= item?.GetHashCode() ?? 0;
		}
		return hash;
	}

	public IEnumerator<T> GetEnumerator() => Items.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public static class ValueSet
{
	public static ValueSet<T> Create<T>(ReadOnlySpan<T> items) => items.Length == 0 ? default : new ValueSet<T>([.. items]);
}
