using System.Diagnostics;
using Domain.Media;
using Domain.Media.Metadata;

namespace DB.Data.Media;

internal class MediaDetailsFactory
{
	public MediaDetailsRow BuildFrom(CachedDetails cached, Guid mediaId)
	{
		var row = NewRow(cached.Details);
		ApplyCommonFields(row, cached, mediaId);
		row.Ratings = [.. cached.Details.Rating.Sources.Select(rating => BuildRating(rating, row))];
		return row;
	}

	public void Update(MediaDetailsRow existing, CachedDetails cached)
	{
		switch (existing, cached.Details)
		{
			case (MovieDetailsRow movie, MovieDetails details):
				movie.Runtime = details.Runtime;
				break;
			case (ShowDetailsRow show, ShowDetails details):
				show.Seasons = details.Seasons;
				show.Episodes = details.Episodes;
				show.RuntimePerEpisode = details.RuntimePerEpisode;
				break;
			default:
				throw new InvalidOperationException($"Cannot update existing {existing.GetType().Name}.");
		}
		ApplyCommonFields(existing, cached, existing.MediaId);
		ReplaceRatings(existing, [.. cached.Details.Rating.Sources]);
	}

	public CachedDetails ToCached(MediaDetailsRow row) => new(Details(row), row.FetchedAt, row.ConfirmedAbsent);

	private static MediaDetails Details(MediaDetailsRow row)
	{
		return Shape(row) with
		{
			Title = row.Title,
			Tagline = row.Tagline,
			Summary = row.Summary,
			PosterUrl = row.PosterUrl,
			BackdropUrl = row.BackdropUrl,
			ReleaseDate = row.ReleaseDate,
			Genres = [.. row.Genres],
			Rating = RatingSummary.From(ToRatings(row.Ratings)),
		};
	}

	private static MediaDetails Shape(MediaDetailsRow row)
	{
		return row switch
		{
			MovieDetailsRow movie => new MovieDetails { Runtime = movie.Runtime },
			ShowDetailsRow show => new ShowDetails
			{
				Seasons = show.Seasons,
				Episodes = show.Episodes,
				RuntimePerEpisode = show.RuntimePerEpisode,
			},
			_ => throw new UnreachableException(),
		};
	}

	private static MediaDetailsRow NewRow(MediaDetails details)
	{
		return details switch
		{
			MovieDetails movie => new MovieDetailsRow { Runtime = movie.Runtime },
			ShowDetails show => new ShowDetailsRow
			{
				Seasons = show.Seasons,
				Episodes = show.Episodes,
				RuntimePerEpisode = show.RuntimePerEpisode,
			},
			UnknownDetails => throw new UnreachableException(),
		};
	}

	private static void ApplyCommonFields(MediaDetailsRow row, CachedDetails cached, Guid mediaId)
	{
		var details = cached.Details;
		row.MediaId = mediaId;
		row.Title = details.Title;
		row.Tagline = details.Tagline;
		row.Summary = details.Summary;
		row.PosterUrl = details.PosterUrl;
		row.BackdropUrl = details.BackdropUrl;
		row.ReleaseDate = details.ReleaseDate;
		row.Genres = [.. details.Genres];
		row.ConfirmedAbsent = cached.ConfirmedAbsent;
		row.FetchedAt = cached.FetchedAt;
	}

	private static void ReplaceRatings(MediaDetailsRow row, IReadOnlyList<MediaRating> ratings)
	{
		row.Ratings.Clear();
		foreach (var rating in ratings)
		{
			row.Ratings.Add(BuildRating(rating, row));
		}
	}

	private static MediaRatingRow BuildRating(MediaRating rating, MediaDetailsRow parent)
	{
		return new()
		{
			Source = rating.Source,
			NormalizedValue = rating.NormalizedValue,
			VoteCount = rating.VoteCount,
			Details = parent,
		};
	}

	private static IReadOnlyList<MediaRating> ToRatings(IEnumerable<MediaRatingRow> ratings)
	{
		return [.. ratings.Select(rating => new MediaRating(rating.Source, rating.NormalizedValue, rating.VoteCount))];
	}
}
