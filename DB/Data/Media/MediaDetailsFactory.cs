using System.Diagnostics;
using Domain.Media;

namespace DB.Data.Media;

internal class MediaDetailsFactory
{
	public MediaDetailsRow BuildFrom(CachedDetails cached, Guid mediaId)
	{
		var row = NewRow(cached.Details);
		ApplyCommonFields(row, cached, mediaId);
		row.Ratings = [.. cached.Details.Ratings.All.Select(rating => BuildRating(rating, row))];
		row.Answers = [.. cached.Answers.Select(answer => BuildAnswer(answer, row))];
		row.PageLinks = [.. cached.Details.PageLinks.Select(link => link.ToPageLinkRow(row))];
		return row;
	}

	public CachedDetails ToCached(MediaDetailsRow row)
	{
		return new(
			Details(row),
			row.RefreshedAt,
			row.AskedAt,
			[.. row.Answers.Select(answer => new ProviderAnswer(answer.Provider, answer.Unfilled, answer.AnsweredAt))]
		);
	}

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
			Ratings = MediaRatings.From(ToRatings(row.Ratings)),
			PageLinks = [.. row.PageLinks.Select(link => link.ToPageLink(row.MediaType))],
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
		row.RefreshedAt = cached.RefreshedAt;
		row.AskedAt = cached.AskedAt;
	}

	private static ProviderAnswerRow BuildAnswer(ProviderAnswer answer, MediaDetailsRow parent)
	{
		return new()
		{
			Provider = answer.Provider,
			Unfilled = answer.Unfilled,
			AnsweredAt = answer.AnsweredAt,
			Details = parent,
		};
	}

	private static MediaRatingRow BuildRating(IMediaRating rating, MediaDetailsRow parent)
	{
		return new()
		{
			Source = rating.Source,
			NormalizedValue = rating.SammlarrValue,
			VoteCount = rating.Votes.ToNullable(),
			Details = parent,
		};
	}

	private static IReadOnlyList<IMediaRating> ToRatings(IEnumerable<MediaRatingRow> ratings)
	{
		return [.. ratings.Select(rating => rating.Source.Card.Rate(rating.NormalizedValue, Optional.Of(rating.VoteCount)))];
	}
}
