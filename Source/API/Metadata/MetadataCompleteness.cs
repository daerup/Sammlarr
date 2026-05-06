using Contracts.Media.Search;
using static API.Metadata.MetadataFields;

namespace API.Metadata;

internal static class MetadataCompleteness
{
	public static bool IsComplete(IMediaDetailResult result) =>
		result switch
		{
			MovieDetailResult movie => HasCommonFields(movie) && HasDuration(movie.Runtime),
			ShowDetailResult show => HasCommonFields(show) && HasCount(show.Seasons) && HasCount(show.Episodes) && HasDuration(show.RuntimePerEpisode),
			_ => false,
		};

	private static bool HasCommonFields(IMediaDetailResult result) =>
		HasText(result.Title)
		&& HasText(result.Tagline)
		&& HasText(result.Summary)
		&& HasText(result.PosterUrl)
		&& HasText(result.BackdropUrl)
		&& HasDate(result.ReleaseDate)
		&& HasItems(result.Genres);
}
