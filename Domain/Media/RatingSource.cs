using System.ComponentModel;
using Domain.Providers;

namespace Domain.Media;

public enum RatingSource
{
	[Description("TMDB")]
	[SuppliedBy<RemoteService>(RemoteService.Tmdb, RemoteService.Plex)]
	Tmdb,

	[Description("IMDb")]
	[SuppliedBy<RemoteService>(RemoteService.Plex)]
	Imdb,

	[Description("Rotten Tomatoes critics")]
	[SuppliedBy<RemoteService>(RemoteService.Plex)]
	RottenTomatoesCritics,

	[Description("Rotten Tomatoes audience")]
	[SuppliedBy<RemoteService>(RemoteService.Plex)]
	RottenTomatoesAudience,
}

public readonly record struct RatingCard(MediaProvider Owner, bool PublishesVotes, Func<float, Optional<int>, IMediaRating> Rate)
{
	internal static RatingCard Of<TRating>()
		where TRating : struct, IOwnedRating<TRating>
	{
		return new RatingCard(TRating.Owner, TRating.PublishesVotes, (value, votes) => TRating.Rate(value, votes));
	}
}

public static class RatingSources
{
	extension(RatingSource source)
	{
		public RatingCard Card
		{
			get
			{
				return source switch
				{
					RatingSource.Tmdb => RatingCard.Of<TmdbRating>(),
					RatingSource.Imdb => RatingCard.Of<ImdbRating>(),
					RatingSource.RottenTomatoesCritics => RatingCard.Of<RottenTomatoesCriticsRating>(),
					RatingSource.RottenTomatoesAudience => RatingCard.Of<RottenTomatoesAudienceRating>(),
				};
			}
		}
	}
}
