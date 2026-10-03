using Domain.Providers;

namespace Domain.Media;

public readonly record struct RatingSummary(Optional<float> Average, ValueSet<MediaRating> Sources)
{
	public static readonly RatingSummary None = new(new None(), []);

	public static RatingSummary From(IReadOnlyList<MediaRating> sources)
	{
		return sources.Count == 0 ? None : new(Some.Of(sources.Average(rating => rating.NormalizedValue)), [.. sources]);
	}
}

public readonly record struct MediaRating(MediaProvider Source, float NormalizedValue, Optional<int> VoteCount)
{
	public MediaRating(MediaProvider source, float normalizedValue)
		: this(source, normalizedValue, new None()) { }
}
