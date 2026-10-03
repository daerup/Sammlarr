using Domain.Media;

namespace API.Tmdb;

internal static class TmdbRatings
{
	public static RatingSummary Of(double? average, int? votes)
	{
		return average is > 0 ? RatingSummary.From([new TmdbRating((float)average.Value, Optional.Of(ProviderValues.Count(votes)))]) : RatingSummary.None;
	}
}
