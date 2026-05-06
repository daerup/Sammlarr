using Contracts.Media.Metadata;
using Contracts.Media.Search;
using static API.Metadata.MetadataFields;

namespace API.Metadata;

internal static class MetadataMerger
{
	public static IMediaDetailResult FillMissing(IMediaDetailResult primary, IMediaDetailResult secondary) =>
		(primary, secondary) switch
		{
			(MovieDetailResult p, MovieDetailResult s) => MergeMovie(p, s),
			(ShowDetailResult p, ShowDetailResult s) => MergeShow(p, s),
		};

	public static IMediaDetailResult WithRatings(IMediaDetailResult details, IReadOnlyList<MediaRating> ratings) =>
		details switch
		{
			MovieDetailResult movie => movie with { Ratings = MergeRatings(movie.Ratings, ratings) },
			ShowDetailResult show => show with { Ratings = MergeRatings(show.Ratings, ratings) },
		};

	private static MovieDetailResult MergeMovie(MovieDetailResult primary, MovieDetailResult secondary) =>
		new()
		{
			SourceId = primary.SourceId,
			AdditionalIds = primary.AdditionalIds,
			Title = Coalesce(primary.Title, secondary.Title),
			Tagline = Coalesce(primary.Tagline, secondary.Tagline),
			Summary = Coalesce(primary.Summary, secondary.Summary),
			PosterUrl = Coalesce(primary.PosterUrl, secondary.PosterUrl),
			BackdropUrl = Coalesce(primary.BackdropUrl, secondary.BackdropUrl),
			ReleaseDate = Coalesce(primary.ReleaseDate, secondary.ReleaseDate),
			Genres = Coalesce(primary.Genres, secondary.Genres),
			Runtime = Coalesce(primary.Runtime, secondary.Runtime),
			Ratings = MergeRatings(primary.Ratings, secondary.Ratings),
		};

	private static ShowDetailResult MergeShow(ShowDetailResult primary, ShowDetailResult secondary) =>
		new()
		{
			SourceId = primary.SourceId,
			AdditionalIds = primary.AdditionalIds,
			Title = Coalesce(primary.Title, secondary.Title),
			Tagline = Coalesce(primary.Tagline, secondary.Tagline),
			Summary = Coalesce(primary.Summary, secondary.Summary),
			PosterUrl = Coalesce(primary.PosterUrl, secondary.PosterUrl),
			BackdropUrl = Coalesce(primary.BackdropUrl, secondary.BackdropUrl),
			ReleaseDate = Coalesce(primary.ReleaseDate, secondary.ReleaseDate),
			Genres = Coalesce(primary.Genres, secondary.Genres),
			Seasons = Coalesce(primary.Seasons, secondary.Seasons),
			Episodes = Coalesce(primary.Episodes, secondary.Episodes),
			RuntimePerEpisode = Coalesce(primary.RuntimePerEpisode, secondary.RuntimePerEpisode),
			Ratings = MergeRatings(primary.Ratings, secondary.Ratings),
		};

	private static IReadOnlyList<MediaRating> MergeRatings(IReadOnlyList<MediaRating> primary, IReadOnlyList<MediaRating> secondary)
	{
		if (secondary.Count == 0)
		{
			return primary;
		}
		if (primary.Count == 0)
		{
			return secondary;
		}

		var bySource = primary.ToDictionary(rating => rating.Source);
		foreach (var rating in secondary)
		{
			bySource.TryAdd(rating.Source, rating);
		}
		return [.. bySource.Values];
	}
}
