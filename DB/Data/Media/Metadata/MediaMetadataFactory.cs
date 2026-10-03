using Domain.Media;
using Domain.Media.Metadata;

namespace DB.Data.Media.Metadata;

internal class MediaMetadataFactory
{
	public MediaMetadataRow BuildFrom(CachedDetails cached, Guid mediaId)
	{
		var entity = NewEntity(cached.Details);
		ApplyCommonFields(entity, cached, mediaId);
		entity.Ratings = [.. cached.Details.Rating.Sources.Select(rating => BuildRating(rating, entity))];
		return entity;
	}

	public void Update(MediaMetadataRow existing, CachedDetails cached)
	{
		switch (existing, cached.Details)
		{
			case (MovieMetadataRow movie, MovieDetails details):
				ApplyCommonFields(movie, cached, movie.MediaId);
				movie.Runtime = details.Runtime ?? TimeSpan.Zero;
				break;
			case (ShowMetadataRow show, ShowDetails details):
				ApplyCommonFields(show, cached, show.MediaId);
				show.Seasons = details.Seasons ?? 0;
				show.Episodes = details.Episodes ?? 0;
				show.RuntimePerEpisode = details.RuntimePerEpisode ?? TimeSpan.Zero;
				break;
			default:
				throw new InvalidOperationException($"Cannot update existing {existing.GetType().Name}.");
		}
		ReplaceRatings(existing, [.. cached.Details.Rating.Sources]);
	}

	public CachedDetails ToCached(MediaMetadataRow entity) => new(Details(entity), entity.LastFetchedAt, entity.ConfirmedAbsent);

	private static MediaDetails Details(MediaMetadataRow entity)
	{
		return Shape(entity) with
		{
			Title = entity.Title.NullIfBlank(),
			Tagline = entity.Tagline.NullIfBlank(),
			Summary = entity.Summary.NullIfBlank(),
			PosterUrl = entity.PosterUrl.NullIfBlank(),
			BackdropUrl = entity.BackdropUrl.NullIfBlank(),
			ReleaseDate = entity.ReleaseDate == DateOnly.MinValue ? null : entity.ReleaseDate,
			Genres = [.. entity.Genres],
			Rating = RatingSummary.From(ToRatings(entity.Ratings)),
		};
	}

	private static MediaDetails Shape(MediaMetadataRow entity)
	{
		return entity switch
		{
			MovieMetadataRow movie => new MovieDetails { Runtime = movie.Runtime == TimeSpan.Zero ? null : movie.Runtime },
			ShowMetadataRow show => new ShowDetails
			{
				Seasons = show.Seasons == 0 ? null : show.Seasons,
				Episodes = show.Episodes == 0 ? null : show.Episodes,
				RuntimePerEpisode = show.RuntimePerEpisode == TimeSpan.Zero ? null : show.RuntimePerEpisode,
			},
			_ => throw new InvalidOperationException($"Unsupported metadata entity type '{entity.GetType().Name}'."),
		};
	}

	private static MediaMetadataRow NewEntity(MediaDetails details)
	{
		return details switch
		{
			MovieDetails movie => new MovieMetadataRow { Runtime = movie.Runtime ?? TimeSpan.Zero },
			ShowDetails show => new ShowMetadataRow
			{
				Seasons = show.Seasons ?? 0,
				Episodes = show.Episodes ?? 0,
				RuntimePerEpisode = show.RuntimePerEpisode ?? TimeSpan.Zero,
			},
			UnknownDetails => throw new InvalidOperationException($"Unsupported media type '{details.Type}'."),
		};
	}

	private static void ApplyCommonFields(MediaMetadataRow entity, CachedDetails cached, Guid mediaId)
	{
		var details = cached.Details;
		entity.MediaId = mediaId;
		entity.Title = details.Title ?? string.Empty;
		entity.Tagline = details.Tagline ?? string.Empty;
		entity.Summary = details.Summary ?? string.Empty;
		entity.PosterUrl = details.PosterUrl ?? string.Empty;
		entity.BackdropUrl = details.BackdropUrl ?? string.Empty;
		entity.ReleaseDate = details.ReleaseDate ?? DateOnly.MinValue;
		entity.Genres = [.. details.Genres];
		entity.ConfirmedAbsent = cached.ConfirmedAbsent;
		entity.LastFetchedAt = cached.FetchedAt;
	}

	private static void ReplaceRatings(MediaMetadataRow entity, IReadOnlyList<MediaRating> ratings)
	{
		entity.Ratings.Clear();
		foreach (var rating in ratings)
		{
			entity.Ratings.Add(BuildRating(rating, entity));
		}
	}

	private static MediaRatingRow BuildRating(MediaRating rating, MediaMetadataRow parent) =>
		new()
		{
			Source = rating.Source,
			NormalizedValue = rating.NormalizedValue,
			VoteCount = rating.VoteCount,
			Metadata = parent,
		};

	private static IReadOnlyList<MediaRating> ToRatings(IEnumerable<MediaRatingRow> ratings) =>
		[.. ratings.Select(rating => new MediaRating(rating.Source, rating.NormalizedValue, rating.VoteCount))];
}
