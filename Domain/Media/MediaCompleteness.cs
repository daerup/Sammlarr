using static Domain.Media.FieldChecks;

namespace Domain.Media;

public static class MediaCompleteness
{
	extension(MediaDetails details)
	{
		public MediaFields Missing
		{
			get
			{
				return Gap(!HasText(details.Title), MediaField.Title)
					| Gap(!HasText(details.Tagline), MediaField.Tagline)
					| Gap(!HasText(details.Summary), MediaField.Summary)
					| Gap(!HasText(details.PosterUrl), MediaField.Poster)
					| Gap(!HasText(details.BackdropUrl), MediaField.Backdrop)
					| Gap(!HasDate(details.ReleaseDate), MediaField.ReleaseDate)
					| Gap(!HasItems(details.Genres), MediaField.Genres)
					| RatingGaps(details.Ratings)
					| details switch
					{
						MovieDetails movie => Gap(!HasDuration(movie.Runtime), MediaField.Runtime),
						ShowDetails show => Gap(!HasCount(show.Seasons), MediaField.Seasons)
							| Gap(!HasCount(show.Episodes), MediaField.Episodes)
							| Gap(!HasDuration(show.RuntimePerEpisode), MediaField.RuntimePerEpisode),
						UnknownDetails => MediaField.None,
					};
			}
		}
	}

	private static MediaField Gap(bool missing, MediaField field) => missing ? field : MediaField.None;

	private static MediaFields RatingGaps(MediaRatings rating)
	{
		return MediaFields.Rated(Enum.GetValues<RatingSource>().Where(source => rating.Of(source).When(Complete).IsNone()));
	}

	private static bool Complete(IMediaRating rating) => rating.Votes.IsSome() || !rating.Source.PublishesVotes();
}
