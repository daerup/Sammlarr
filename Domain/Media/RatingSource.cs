using System.ComponentModel;
using Domain.Providers;

namespace Domain.Media;

public enum RatingSource
{
	[Description("TMDB")]
	[OwnedBy<MediaProvider>(MediaProvider.Tmdb)]
	[SuppliedBy<MediaProvider>(MediaProvider.Tmdb, MediaProvider.Plex)]
	[Votes]
	Tmdb,

	[Description("IMDb")]
	[OwnedBy<MediaProvider>(MediaProvider.Imdb)]
	[SuppliedBy<MediaProvider>(MediaProvider.Plex)]
	[Votes]
	Imdb,

	[Description("Rotten Tomatoes critics")]
	[OwnedBy<MediaProvider>(MediaProvider.RottenTomatoes)]
	[SuppliedBy<MediaProvider>(MediaProvider.Plex)]
	RottenTomatoesCritics,

	[Description("Rotten Tomatoes audience")]
	[OwnedBy<MediaProvider>(MediaProvider.RottenTomatoes)]
	[SuppliedBy<MediaProvider>(MediaProvider.Plex)]
	RottenTomatoesAudience,
}

public static class RatingSources
{
	extension(RatingSource source)
	{
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
