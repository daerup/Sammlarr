using Domain.Media;

namespace API.Tmdb;

internal static class TmdbRatings
{
	public static MediaRatings Of(double? average, int? votes)
	{
		return average is > 0 ? MediaRatings.From([new TmdbRating((float)average.Value, Optional.Of(ProviderValues.Count(votes)))]) : MediaRatings.None;
	}
}
