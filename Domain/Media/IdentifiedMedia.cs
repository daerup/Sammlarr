namespace Domain.Media;

public union IdentifiedMedia(IMedia, IPartialMedia)
{
	public static IdentifiedMedia From(IPartialMedia partial) =>
		partial switch
		{
			PartialMovie movie when partial.IsComplete => new Movie
			{
				Identity = movie.Identity,
				Title = movie.Title!,
				Tagline = movie.Tagline!,
				Summary = movie.Summary!,
				PosterUrl = movie.PosterUrl!,
				BackdropUrl = movie.BackdropUrl!,
				ReleaseDate = movie.ReleaseDate!.Value,
				Genres = movie.Genres,
				Rating = movie.Rating,
				Runtime = movie.Runtime!.Value,
			},
			PartialShow show when partial.IsComplete => new Show
			{
				Identity = show.Identity,
				Title = show.Title!,
				Tagline = show.Tagline!,
				Summary = show.Summary!,
				PosterUrl = show.PosterUrl!,
				BackdropUrl = show.BackdropUrl!,
				ReleaseDate = show.ReleaseDate!.Value,
				Genres = show.Genres,
				Rating = show.Rating,
				Seasons = show.Seasons!.Value,
				Episodes = show.Episodes!.Value,
				RuntimePerEpisode = show.RuntimePerEpisode!.Value,
			},
			_ => partial,
		};
}
