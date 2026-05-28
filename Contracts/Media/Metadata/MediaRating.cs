namespace Contracts.Media.Metadata;

public readonly record struct MediaRating(RatingSource Source, float NormalizedValue, int? VoteCount = null);

public enum RatingSource
{
	Tmdb,
}
