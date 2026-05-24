using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using static Core.Media.Metadata.MetadataFields;

namespace Core.Media.Metadata;

internal static class MetadataMerger
{
	public static IMediaDetail FillMissing(IMediaDetail primary, IMediaDetail secondary) =>
		(primary, secondary) switch
		{
			(MovieDetail p, MovieDetail s) => MergeMovie(p, s),
			(ShowDetail p, ShowDetail s) => MergeShow(p, s),
		};

	public static IMediaDetail WithRatings(IMediaDetail details, IReadOnlyList<MediaRating> ratings) =>
		details switch
		{
			MovieDetail movie => movie with { Ratings = MergeRatings(movie.Ratings, ratings) },
			ShowDetail show => show with { Ratings = MergeRatings(show.Ratings, ratings) },
		};

	private static MovieDetail MergeMovie(MovieDetail primary, MovieDetail secondary) =>
		new()
		{
			Identity = MergeIdentity(primary.Identity, secondary.Identity),
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

	private static ShowDetail MergeShow(ShowDetail primary, ShowDetail secondary) =>
		new()
		{
			Identity = MergeIdentity(primary.Identity, secondary.Identity),
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

	private static MediaIdentity MergeIdentity(MediaIdentity primary, MediaIdentity secondary) =>
		primary with
		{
			Externals = UnionExternals(primary.Externals, secondary.Externals),
		};

	private static IReadOnlyList<IExternalMediaId> UnionExternals(IReadOnlyList<IExternalMediaId> primary, IReadOnlyList<IExternalMediaId> secondary)
	{
		if (secondary.Count == 0)
		{
			return primary;
		}
		var known = primary.ToHashSet();
		var added = secondary.Where(known.Add).ToList();
		return added.Count == 0 ? primary : [.. primary, .. added];
	}

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
