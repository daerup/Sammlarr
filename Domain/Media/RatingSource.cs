using System.ComponentModel;
using Domain.Providers;

namespace Domain.Media;

public enum RatingSource
{
	[Description("TMDB")]
	[SuppliedBy<RemoteService>(RemoteService.Tmdb, RemoteService.Plex, RemoteService.Mdblist)]
	Tmdb,

	[Description("IMDb")]
	[SuppliedBy<RemoteService>(RemoteService.Plex, RemoteService.Mdblist, RemoteService.ImdbDatasets)]
	Imdb,

	[Description("Rotten Tomatoes critics")]
	[SuppliedBy<RemoteService>(RemoteService.Plex, RemoteService.Mdblist)]
	RottenTomatoesCritics,

	[Description("Rotten Tomatoes audience")]
	[SuppliedBy<RemoteService>(RemoteService.Plex, RemoteService.Mdblist)]
	RottenTomatoesAudience,

	[Description("Metacritic")]
	[SuppliedBy<RemoteService>(RemoteService.Mdblist)]
	Metacritic,

	[Description("Letterboxd")]
	[SuppliedBy<RemoteService>(RemoteService.Mdblist)]
	Letterboxd,

	[Description("Trakt")]
	[SuppliedBy<RemoteService>(RemoteService.Mdblist)]
	Trakt,

	[Description("MyAnimeList")]
	[SuppliedBy<RemoteService>(RemoteService.Mdblist)]
	MyAnimeList,
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
					RatingSource.Metacritic => RatingCard.Of<MetacriticRating>(),
					RatingSource.Letterboxd => RatingCard.Of<LetterboxdRating>(),
					RatingSource.Trakt => RatingCard.Of<TraktRating>(),
					RatingSource.MyAnimeList => RatingCard.Of<MyAnimeListRating>(),
				};
			}
		}
	}
}
