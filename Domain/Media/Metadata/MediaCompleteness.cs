using static Domain.Media.Metadata.MediaFields;

namespace Domain.Media.Metadata;

public static class MediaCompleteness
{
	extension(IPartialMedia partial)
	{
		public MediaField Missing =>
			Gap(!HasText(partial.Title), MediaField.Title)
			| Gap(!HasText(partial.Tagline), MediaField.Tagline)
			| Gap(!HasText(partial.Summary), MediaField.Summary)
			| Gap(!HasText(partial.PosterUrl), MediaField.Poster)
			| Gap(!HasText(partial.BackdropUrl), MediaField.Backdrop)
			| Gap(!HasDate(partial.ReleaseDate), MediaField.ReleaseDate)
			| Gap(!HasItems(partial.Genres), MediaField.Genres)
			| Gap(!HasItems(partial.Rating.Sources), MediaField.Rating)
			| partial switch
			{
				PartialMovie movie => Gap(!HasDuration(movie.Runtime), MediaField.Runtime),
				PartialShow show => Gap(!HasCount(show.Seasons), MediaField.Seasons)
					| Gap(!HasCount(show.Episodes), MediaField.Episodes)
					| Gap(!HasDuration(show.RuntimePerEpisode), MediaField.RuntimePerEpisode),
				_ => MediaField.None,
			};

		public bool IsComplete => partial.Missing is MediaField.None;

		public IdentifiedMedia Confirming(MediaField fields) =>
			partial switch
			{
				PartialMovie movie => IdentifiedMedia.From(movie with { ConfirmedMissing = fields }),
				PartialShow show => IdentifiedMedia.From(show with { ConfirmedMissing = fields }),
				_ => IdentifiedMedia.From(partial),
			};
	}

	private static MediaField Gap(bool missing, MediaField field) => missing ? field : MediaField.None;
}
