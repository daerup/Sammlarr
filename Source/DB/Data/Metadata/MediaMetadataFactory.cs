using Contracts;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;

namespace DB.Data.Metadata;

internal class MediaMetadataFactory(IDateTimeProvider dateTimeProvider)
{
	public TEntity BuildFrom<TEntity>(IMediaDetailResult details, Guid groupId)
		where TEntity : MediaMetadataEntity
	{
		MediaMetadataEntity entity = details switch
		{
			MovieDetailResult movie => new MovieMetadataEntity { Runtime = movie.Runtime },
			ShowDetailResult show => new ShowMetadataEntity
			{
				Seasons = show.Seasons,
				Episodes = show.Episodes,
				RuntimePerEpisode = show.RuntimePerEpisode,
			},
			_ => throw new InvalidOperationException($"Unsupported media detail result type '{details.GetType().Name}'."),
		};
		ApplyCommonFields(entity, details, groupId);
		entity.LastFetchedAt = dateTimeProvider.Now;
		entity.Ratings = [.. details.Ratings.Select(rating => BuildRating(rating, entity))];
		return (TEntity)entity;
	}

	public void Update(MediaMetadataEntity existing, IMediaDetailResult details)
	{
		switch (existing, details)
		{
			case (MovieMetadataEntity movie, MovieDetailResult movieDetails):
				ApplyCommonFields(movie, movieDetails, movie.MediaId);
				movie.Runtime = movieDetails.Runtime;
				ReplaceRatings(movie, movieDetails.Ratings);
				break;
			case (ShowMetadataEntity show, ShowDetailResult showDetails):
				ApplyCommonFields(show, showDetails, show.MediaId);
				show.Seasons = showDetails.Seasons;
				show.Episodes = showDetails.Episodes;
				show.RuntimePerEpisode = showDetails.RuntimePerEpisode;
				ReplaceRatings(show, showDetails.Ratings);
				break;
			default:
				throw new InvalidOperationException($"Cannot update existing {existing.GetType().Name} with {details.GetType().Name}.");
		}
		existing.LastFetchedAt = dateTimeProvider.Now;
	}

	public IMediaDetailResult ToDetailResult(MediaMetadataEntity entity, MediaId id, IReadOnlyList<IExternalMediaId> externals) =>
		entity switch
		{
			MovieMetadataEntity movie => new MovieDetailResult
			{
				Id = id,
				Externals = externals,
				Title = movie.Title,
				Tagline = movie.Tagline,
				Summary = movie.Summary,
				PosterUrl = movie.PosterUrl,
				BackdropUrl = movie.BackdropUrl,
				Runtime = movie.Runtime,
				ReleaseDate = movie.ReleaseDate,
				Genres = movie.Genres,
				Ratings = ToRatings(movie.Ratings),
			},
			ShowMetadataEntity show => new ShowDetailResult
			{
				Id = id,
				Externals = externals,
				Title = show.Title,
				Tagline = show.Tagline,
				Summary = show.Summary,
				PosterUrl = show.PosterUrl,
				BackdropUrl = show.BackdropUrl,
				RuntimePerEpisode = show.RuntimePerEpisode,
				ReleaseDate = show.ReleaseDate,
				Genres = show.Genres,
				Seasons = show.Seasons,
				Episodes = show.Episodes,
				Ratings = ToRatings(show.Ratings),
			},
			_ => throw new InvalidOperationException($"Unsupported metadata entity type '{entity.GetType().Name}'."),
		};

	private static void ReplaceRatings(MediaMetadataEntity entity, IReadOnlyList<MediaRating> ratings)
	{
		entity.Ratings.Clear();
		foreach (var rating in ratings)
		{
			entity.Ratings.Add(BuildRating(rating, entity));
		}
	}

	private static void ApplyCommonFields(MediaMetadataEntity entity, IMediaDetailResult source, Guid mediaId)
	{
		entity.MediaId = mediaId;
		entity.Title = source.Title;
		entity.Tagline = source.Tagline;
		entity.Summary = source.Summary;
		entity.PosterUrl = source.PosterUrl;
		entity.BackdropUrl = source.BackdropUrl;
		entity.ReleaseDate = source.ReleaseDate;
		entity.Genres = [.. source.Genres];
	}

	private static MediaRatingEntity BuildRating(MediaRating rating, MediaMetadataEntity parent) =>
		new()
		{
			Source = rating.Source,
			NormalizedValue = rating.NormalizedValue,
			VoteCount = rating.VoteCount,
			Metadata = parent,
		};

	private static IReadOnlyList<MediaRating> ToRatings(IEnumerable<MediaRatingEntity> ratings) =>
		[.. ratings.Select(rating => new MediaRating(rating.Source, rating.NormalizedValue, rating.VoteCount))];
}
