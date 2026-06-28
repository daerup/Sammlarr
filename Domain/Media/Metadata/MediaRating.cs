namespace Domain.Media.Metadata;

public enum RatingSource
{
	Tmdb,
}

public readonly record struct RatingSummary(float? Average, ValueSet<MediaRating> Sources)
{
	public static readonly RatingSummary None = new RatingSummary(null, []);

	public static RatingSummary From(IReadOnlyList<MediaRating> sources) =>
		sources.Count == 0 ? None : new(sources.Average(rating => rating.NormalizedValue), [.. sources]);
}

public readonly record struct MediaRating(RatingSource Source, float NormalizedValue, int? VoteCount = null);
