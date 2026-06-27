namespace Domain.Media.Metadata;

public readonly record struct MediaRating(RatingSource Source, float NormalizedValue, int? VoteCount = null);

public enum RatingSource
{
	Tmdb,
}

public readonly record struct RatingSummary(float? Average, ValueSet<MediaRating> Sources)
{
	public static readonly RatingSummary None = new(null, default);

	public static RatingSummary From(IReadOnlyList<MediaRating> sources) =>
		sources.Count == 0 ? None : new((float)sources.Average(rating => rating.NormalizedValue), [.. sources]);
}
