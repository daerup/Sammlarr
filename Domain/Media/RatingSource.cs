using System.ComponentModel;
using Domain.Providers;

namespace Domain.Media;

public enum RatingSource
{
	[Description("TMDB")]
	[SuppliedBy<MediaProvider>(MediaProvider.Tmdb, MediaProvider.Plex)]
	Tmdb,

	[Description("IMDb")]
	[SuppliedBy<MediaProvider>(MediaProvider.Plex)]
	Imdb,

	[Description("Rotten Tomatoes critics")]
	[SuppliedBy<MediaProvider>(MediaProvider.Plex)]
	RottenTomatoesCritics,

	[Description("Rotten Tomatoes audience")]
	[SuppliedBy<MediaProvider>(MediaProvider.Plex)]
	RottenTomatoesAudience,
}

public enum RatingMark
{
	Tmdb,
	Imdb,
	Fresh,
	Rotten,
	Upright,
	Spilled,
}

public static class RatingSources
{
	extension(RatingSource source)
	{
		public MediaField Field
		{
			get
			{
				return source switch
				{
					RatingSource.Tmdb => MediaField.TmdbRating,
					RatingSource.Imdb => MediaField.ImdbRating,
					RatingSource.RottenTomatoesCritics => MediaField.RottenTomatoesCriticsRating,
					RatingSource.RottenTomatoesAudience => MediaField.RottenTomatoesAudienceRating,
				};
			}
		}

		public bool PublishesVotes
		{
			get
			{
				return source switch
				{
					RatingSource.Tmdb => true,
					RatingSource.Imdb => true,
					RatingSource.RottenTomatoesCritics => false,
					RatingSource.RottenTomatoesAudience => false,
				};
			}
		}

		public IMediaRating Rate(float sammlarrValue, Optional<int> votes)
		{
			return source switch
			{
				RatingSource.Tmdb => new TmdbRating(sammlarrValue, votes),
				RatingSource.Imdb => new ImdbRating(sammlarrValue, votes),
				RatingSource.RottenTomatoesCritics => RottenTomatoesCriticsRating.Of(sammlarrValue),
				RatingSource.RottenTomatoesAudience => RottenTomatoesAudienceRating.Of(sammlarrValue),
			};
		}
	}
}
