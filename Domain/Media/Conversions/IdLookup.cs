using Domain.Providers;

namespace Domain.Media.Conversions;

public readonly union IdLookup(ById, ByHints)
{
	public static bool operator ==(IdLookup left, IdLookup right) => Equals(left.Value, right.Value);

	public static bool operator !=(IdLookup left, IdLookup right) => !Equals(left.Value, right.Value);

	public override bool Equals(object? obj) => obj is IdLookup other && this == other;

	public override int GetHashCode() => Value?.GetHashCode() ?? 0;
}

public readonly record struct ById(ValueSet<MediaProvider> Accepts, ValueSet<MediaProvider> Yields, int Cost, bool GivesHints = true, bool AsksOnce = false);

public readonly record struct ByHints(ValueSet<MediaProvider> Yields, int Cost);
