using static Domain.Media.MediaFields;

namespace Domain.Media;

public static class MediaMerger
{
	extension(MediaDetails details)
	{
		public MediaDetails FillFrom(MediaDetails other)
		{
			return (details, other) switch
			{
				(MovieDetails mine, MovieDetails theirs) => (MovieDetails)Common(mine, theirs) with { Runtime = mine.Runtime ?? theirs.Runtime },
				(ShowDetails mine, ShowDetails theirs) => (ShowDetails)Common(mine, theirs) with
				{
					Seasons = mine.Seasons ?? theirs.Seasons,
					Episodes = mine.Episodes ?? theirs.Episodes,
					RuntimePerEpisode = mine.RuntimePerEpisode ?? theirs.RuntimePerEpisode,
				},
				(UnknownDetails mine, UnknownDetails theirs) => Common(mine, theirs),
				_ => details,
			};
		}
	}

	private static MediaDetails Common(MediaDetails mine, MediaDetails theirs)
	{
		return mine with
		{
			Title = mine.Title ?? theirs.Title,
			Tagline = mine.Tagline ?? theirs.Tagline,
			Summary = mine.Summary ?? theirs.Summary,
			PosterUrl = mine.PosterUrl ?? theirs.PosterUrl,
			BackdropUrl = mine.BackdropUrl ?? theirs.BackdropUrl,
			ReleaseDate = mine.ReleaseDate ?? theirs.ReleaseDate,
			Genres = Coalesce(mine.Genres, theirs.Genres),
			Rating = mine.Rating.FillFrom(theirs.Rating),
		};
	}
}
