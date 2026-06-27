using static Domain.Media.Metadata.MediaFields;

namespace Domain.Media.Metadata;

public static class MediaMerger
{
	extension(IPartialMedia partial)
	{
		public MediaLookup MergeWith(IPartialMedia other)
		{
			var merged = Fill(partial, other);
			return merged.IsComplete ? Promote(merged) : new MediaLookup(merged);
		}

		public MediaLookup MergeWith(IMedia other) =>
			(partial, other) switch
			{
				(PartialMovie p, Movie m) => new MediaLookup(
					m with
					{
						Identity = m.Identity with { Externals = [.. p.Identity.Externals, .. m.Identity.Externals] },
						Title = p.Title ?? m.Title,
						Tagline = p.Tagline ?? m.Tagline,
						Summary = p.Summary ?? m.Summary,
						PosterUrl = p.PosterUrl ?? m.PosterUrl,
						BackdropUrl = p.BackdropUrl ?? m.BackdropUrl,
						ReleaseDate = p.ReleaseDate ?? m.ReleaseDate,
						Genres = Coalesce(p.Genres, m.Genres),
						Rating = Coalesce(p.Rating, m.Rating),
						Runtime = p.Runtime ?? m.Runtime,
					}
				),
				(PartialShow p, Show s) => new MediaLookup(
					s with
					{
						Identity = s.Identity with { Externals = [.. p.Identity.Externals, .. s.Identity.Externals] },
						Title = p.Title ?? s.Title,
						Tagline = p.Tagline ?? s.Tagline,
						Summary = p.Summary ?? s.Summary,
						PosterUrl = p.PosterUrl ?? s.PosterUrl,
						BackdropUrl = p.BackdropUrl ?? s.BackdropUrl,
						ReleaseDate = p.ReleaseDate ?? s.ReleaseDate,
						Genres = Coalesce(p.Genres, s.Genres),
						Rating = Coalesce(p.Rating, s.Rating),
						Seasons = p.Seasons ?? s.Seasons,
						Episodes = p.Episodes ?? s.Episodes,
						RuntimePerEpisode = p.RuntimePerEpisode ?? s.RuntimePerEpisode,
					}
				),
				_ => new MediaLookup(other),
			};
	}

	public static MediaLookup WithRatings(MediaLookup media, IReadOnlyList<MediaRating> ratings)
	{
		if (ratings.Count == 0)
		{
			return media;
		}
		var summary = RatingSummary.From(ratings);
		return media switch
		{
			Movie movie => new MediaLookup(movie with { Rating = summary }),
			Show show => new MediaLookup(show with { Rating = summary }),
			PartialMovie movie => new MediaLookup(movie with { Rating = summary }),
			PartialShow show => new MediaLookup(show with { Rating = summary }),
			_ => media,
		};
	}

	private static IPartialMedia Fill(IPartialMedia primary, IPartialMedia secondary) =>
		(primary, secondary) switch
		{
			(PartialMovie p, PartialMovie s) => p with
			{
				Identity = p.Identity with { Externals = [.. p.Identity.Externals, .. s.Identity.Externals] },
				Title = p.Title ?? s.Title,
				Tagline = p.Tagline ?? s.Tagline,
				Summary = p.Summary ?? s.Summary,
				PosterUrl = p.PosterUrl ?? s.PosterUrl,
				BackdropUrl = p.BackdropUrl ?? s.BackdropUrl,
				ReleaseDate = p.ReleaseDate ?? s.ReleaseDate,
				Genres = Coalesce(p.Genres, s.Genres),
				Rating = Coalesce(p.Rating, s.Rating),
				Runtime = p.Runtime ?? s.Runtime,
			},
			(PartialShow p, PartialShow s) => p with
			{
				Identity = p.Identity with { Externals = [.. p.Identity.Externals, .. s.Identity.Externals] },
				Title = p.Title ?? s.Title,
				Tagline = p.Tagline ?? s.Tagline,
				Summary = p.Summary ?? s.Summary,
				PosterUrl = p.PosterUrl ?? s.PosterUrl,
				BackdropUrl = p.BackdropUrl ?? s.BackdropUrl,
				ReleaseDate = p.ReleaseDate ?? s.ReleaseDate,
				Genres = Coalesce(p.Genres, s.Genres),
				Rating = Coalesce(p.Rating, s.Rating),
				Seasons = p.Seasons ?? s.Seasons,
				Episodes = p.Episodes ?? s.Episodes,
				RuntimePerEpisode = p.RuntimePerEpisode ?? s.RuntimePerEpisode,
			},
			_ => primary,
		};

	private static MediaLookup Promote(IPartialMedia partial) =>
		partial switch
		{
			PartialMovie movie => new MediaLookup(
				new Movie
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
				}
			),
			PartialShow show => new MediaLookup(
				new Show
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
				}
			),
			_ => new MediaLookup(partial),
		};
}
