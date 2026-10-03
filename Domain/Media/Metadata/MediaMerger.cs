using static Domain.Media.Metadata.MediaFields;

namespace Domain.Media.Metadata;

public static class MediaMerger
{
	extension(IPartialMedia partial)
	{
		public IdentifiedMedia MergeWith(IdentifiedMedia other) =>
			other switch
			{
				IMedia complete => partial.MergeWith(complete),
				IPartialMedia incomplete => partial.MergeWith(incomplete),
			};

		public IdentifiedMedia MergeWith(IPartialMedia other) => IdentifiedMedia.From(Fill(partial, other));

		public IdentifiedMedia MergeWith(IMedia other) =>
			(partial, other) switch
			{
				(PartialMovie p, Movie m) => m with
				{
					Identity = p.Identity with { Externals = p.Identity.Externals + m.Identity.Externals },
					Title = p.Title ?? m.Title,
					Tagline = p.Tagline ?? m.Tagline,
					Summary = p.Summary ?? m.Summary,
					PosterUrl = p.PosterUrl ?? m.PosterUrl,
					BackdropUrl = p.BackdropUrl ?? m.BackdropUrl,
					ReleaseDate = p.ReleaseDate ?? m.ReleaseDate,
					Genres = Coalesce(p.Genres, m.Genres),
					Rating = Coalesce(p.Rating, m.Rating),
					Runtime = p.Runtime ?? m.Runtime,
				},
				(PartialShow p, Show s) => s with
				{
					Identity = p.Identity with { Externals = p.Identity.Externals + s.Identity.Externals },
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
				_ => other,
			};
	}

	private static IPartialMedia Fill(IPartialMedia primary, IPartialMedia secondary) =>
		(primary, secondary) switch
		{
			(PartialMovie p, PartialMovie s) => p with
			{
				Identity = p.Identity with { Externals = p.Identity.Externals + s.Identity.Externals },
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
				Identity = p.Identity with { Externals = p.Identity.Externals + s.Identity.Externals },
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
}
