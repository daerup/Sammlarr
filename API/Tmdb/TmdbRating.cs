using Domain.Media.Metadata;
using Domain.Providers;

namespace API.Tmdb;

internal static class TmdbRating
{
	public static RatingSummary Of(double? average, int? votes)
	{
		return average is > 0 ? RatingSummary.From([new MediaRating(MediaProvider.Tmdb, (float)average.Value, ProviderValues.Count(votes))]) : RatingSummary.None;
	}
}
