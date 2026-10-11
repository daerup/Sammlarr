using static Domain.Media.FieldChecks;

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

		public MediaDetails Only(MediaFields kept)
		{
			var common = details with
			{
				Title = Kept(kept, MediaField.Title, details.Title),
				Tagline = Kept(kept, MediaField.Tagline, details.Tagline),
				Summary = Kept(kept, MediaField.Summary, details.Summary),
				PosterUrl = Kept(kept, MediaField.Poster, details.PosterUrl),
				BackdropUrl = Kept(kept, MediaField.Backdrop, details.BackdropUrl),
				ReleaseDate = Kept(kept, MediaField.ReleaseDate, details.ReleaseDate),
				Genres = kept.Details.Overlaps(MediaField.Genres) ? details.Genres : [],
				Ratings = MediaRatings.From(details.Ratings.All.Where(rating => kept.Ratings.Contains(rating.Source))),
			};
			return common switch
			{
				MovieDetails movie => movie with { Runtime = Kept(kept, MediaField.Runtime, movie.Runtime) },
				ShowDetails show => show with
				{
					Seasons = Kept(kept, MediaField.Seasons, show.Seasons),
					Episodes = Kept(kept, MediaField.Episodes, show.Episodes),
					RuntimePerEpisode = Kept(kept, MediaField.RuntimePerEpisode, show.RuntimePerEpisode),
				},
				UnknownDetails unknown => unknown,
			};
		}

		public MediaDetails Without(MediaFields dropped) => details.Only(MediaFields.Everything.Except(dropped));
	}

	private static T? Kept<T>(MediaFields kept, MediaField field, T? value) => kept.Details.Overlaps(field) ? value : default;

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
			Ratings = mine.Ratings.FillFrom(theirs.Ratings),
			PageLinks = mine.PageLinks + theirs.PageLinks.Where(link => mine.PageLinks.All(held => held.Source != link.Source)),
		};
	}
}
