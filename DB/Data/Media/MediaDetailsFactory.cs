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
		ReplaceRatings(existing, [.. cached.Details.Ratings.All]);
		ReplaceAnswers(existing, cached.Answers);
	}

	public CachedDetails ToCached(MediaDetailsRow row)
	{
		return new(Details(row), row.RefreshedAt, row.AskedAt, [.. row.Answers.Select(answer => new ProviderAnswer(answer.Provider, answer.Unfilled))]);
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

	private static void ReplaceRatings(MediaDetailsRow row, IReadOnlyList<IMediaRating> ratings)
	{
		var asked = row.Ratings.FirstOrNone(rating => rating.RottenTomatoesAskedAt.HasValue);
		row.Ratings.Clear();
		foreach (var rating in ratings)
		{
			var built = BuildRating(rating, row);
			asked.When(_ => rating.Source.IsRottenTomatoes).Apply(answer => KeepAnswer(built, answer));
			row.Ratings.Add(built);
		}
	}

	private static void ReplaceAnswers(MediaDetailsRow row, IEnumerable<ProviderAnswer> answers)
	{
		row.Answers.Clear();
		row.Answers.AddRange(answers.Select(answer => BuildAnswer(answer, row)));
	}

	private static ProviderAnswerRow BuildAnswer(ProviderAnswer answer, MediaDetailsRow parent)
	{
		return new()
		{
			Provider = answer.Provider,
			Unfilled = answer.Unfilled,
			Details = parent,
		};
	}

	private static void KeepAnswer(MediaRatingRow rating, MediaRatingRow answer)
	{
		rating.RottenTomatoesPath = answer.RottenTomatoesPath;
		rating.RottenTomatoesAskedAt = answer.RottenTomatoesAskedAt;
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
		return [.. ratings.Select(rating => rating.Source.Rate(rating.NormalizedValue, Optional.Of(rating.VoteCount)))];
	}
}
