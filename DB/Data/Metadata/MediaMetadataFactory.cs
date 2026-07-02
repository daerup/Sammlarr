using Contracts;
using Domain;
using Domain.Media;
using Domain.Media.Metadata;

namespace DB.Data.Metadata;

internal class MediaMetadataFactory(IDateTimeProvider dateTimeProvider)
{
	public MediaMetadataRow BuildFrom(IdentifiedMedia media, Guid mediaId)
	{
		var entity = NewEntity(media);
		ApplyCommonFields(entity, media, mediaId);
		entity.LastFetchedAt = dateTimeProvider.Now;
		entity.Ratings = [.. Ratings(media).Select(rating => BuildRating(rating, entity))];
		return entity;
	}

	public void Update(MediaMetadataRow existing, IdentifiedMedia media)
	{
		switch (existing)
		{
			case MovieMetadataRow movie when media is Movie or PartialMovie:
				ApplyCommonFields(movie, media, movie.MediaId);
				movie.Runtime = Runtime(media);
				ReplaceRatings(movie, Ratings(media));
				break;
			case ShowMetadataRow show when media is Show or PartialShow:
				ApplyCommonFields(show, media, show.MediaId);
				show.Seasons = Seasons(media);
				show.Episodes = Episodes(media);
				show.RuntimePerEpisode = RuntimePerEpisode(media);
				ReplaceRatings(show, Ratings(media));
				break;
			default:
				throw new InvalidOperationException($"Cannot update existing {existing.GetType().Name}.");
		}
		existing.LastFetchedAt = dateTimeProvider.Now;
	}

	public IdentifiedMedia ToStored(MediaMetadataRow entity, MediaIdentity identity)
	{
		IPartialMedia partial = entity switch
		{
			MovieMetadataRow movie => new PartialMovie
			{
				Identity = identity,
				Title = movie.Title.NullIfBlank(),
				Tagline = movie.Tagline.NullIfBlank(),
				Summary = movie.Summary.NullIfBlank(),
				PosterUrl = movie.PosterUrl.NullIfBlank(),
				BackdropUrl = movie.BackdropUrl.NullIfBlank(),
				ReleaseDate = movie.ReleaseDate == DateOnly.MinValue ? null : movie.ReleaseDate,
				Genres = [.. movie.Genres],
				Rating = RatingSummary.From(ToRatings(movie.Ratings)),
				Runtime = movie.Runtime == TimeSpan.Zero ? null : movie.Runtime,
			},
			ShowMetadataRow show => new PartialShow
			{
				Identity = identity,
				Title = show.Title.NullIfBlank(),
				Tagline = show.Tagline.NullIfBlank(),
				Summary = show.Summary.NullIfBlank(),
				PosterUrl = show.PosterUrl.NullIfBlank(),
				BackdropUrl = show.BackdropUrl.NullIfBlank(),
				ReleaseDate = show.ReleaseDate == DateOnly.MinValue ? null : show.ReleaseDate,
				Genres = [.. show.Genres],
				Rating = RatingSummary.From(ToRatings(show.Ratings)),
				Seasons = show.Seasons == 0 ? null : show.Seasons,
				Episodes = show.Episodes == 0 ? null : show.Episodes,
				RuntimePerEpisode = show.RuntimePerEpisode == TimeSpan.Zero ? null : show.RuntimePerEpisode,
			},
			_ => throw new InvalidOperationException($"Unsupported metadata entity type '{entity.GetType().Name}'."),
		};
		return IdentifiedMedia.From(partial);
	}

	private static MediaMetadataRow NewEntity(IdentifiedMedia media) =>
		media switch
		{
			Movie or PartialMovie => new MovieMetadataRow { Runtime = Runtime(media) },
			Show or PartialShow => new ShowMetadataRow
			{
				Seasons = Seasons(media),
				Episodes = Episodes(media),
				RuntimePerEpisode = RuntimePerEpisode(media),
			},
			_ => throw new InvalidOperationException($"Unsupported media type '{media.GetType().Name}'."),
		};

	private static void ApplyCommonFields(MediaMetadataRow entity, IdentifiedMedia media, Guid mediaId)
	{
		entity.MediaId = mediaId;
		switch (media)
		{
			case IMedia resolved:
				entity.Title = resolved.Title;
				entity.Tagline = resolved.Tagline;
				entity.Summary = resolved.Summary;
				entity.PosterUrl = resolved.PosterUrl;
				entity.BackdropUrl = resolved.BackdropUrl;
				entity.ReleaseDate = resolved.ReleaseDate;
				entity.Genres = [.. resolved.Genres];
				break;
			case IPartialMedia partial:
				entity.Title = partial.Title ?? string.Empty;
				entity.Tagline = partial.Tagline ?? string.Empty;
				entity.Summary = partial.Summary ?? string.Empty;
				entity.PosterUrl = partial.PosterUrl ?? string.Empty;
				entity.BackdropUrl = partial.BackdropUrl ?? string.Empty;
				entity.ReleaseDate = partial.ReleaseDate ?? DateOnly.MinValue;
				entity.Genres = [.. partial.Genres];
				break;
		}
	}

	private static TimeSpan Runtime(IdentifiedMedia media) =>
		media switch
		{
			Movie movie => movie.Runtime,
			PartialMovie movie => movie.Runtime ?? TimeSpan.Zero,
			_ => TimeSpan.Zero,
		};

	private static int Seasons(IdentifiedMedia media) =>
		media switch
		{
			Show show => show.Seasons,
			PartialShow show => show.Seasons ?? 0,
			_ => 0,
		};

	private static int Episodes(IdentifiedMedia media) =>
		media switch
		{
			Show show => show.Episodes,
			PartialShow show => show.Episodes ?? 0,
			_ => 0,
		};

	private static TimeSpan RuntimePerEpisode(IdentifiedMedia media) =>
		media switch
		{
			Show show => show.RuntimePerEpisode,
			PartialShow show => show.RuntimePerEpisode ?? TimeSpan.Zero,
			_ => TimeSpan.Zero,
		};

	private static IReadOnlyList<MediaRating> Ratings(IdentifiedMedia media) =>
		media switch
		{
			IMedia resolved => [.. resolved.Rating.Sources],
			IPartialMedia partial => [.. partial.Rating.Sources],
			_ => [],
		};

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
