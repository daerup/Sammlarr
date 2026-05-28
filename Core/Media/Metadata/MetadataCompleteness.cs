using Contracts.Media.Search;
using static Core.Media.Metadata.MetadataFields;

namespace Core.Media.Metadata;

internal static class MetadataCompleteness
{
	public static IReadOnlyList<string> GetMissingFields(IMediaDetail result)
	{
		var missing = new List<string>();
		if (result is MovieDetail movie)
		{
			AddMissingCommonFields(movie, missing);
			if (!HasDuration(movie.Runtime))
				missing.Add(nameof(MovieDetail.Runtime));
		}
		else if (result is ShowDetail show)
		{
			AddMissingCommonFields(show, missing);
			if (!HasCount(show.Seasons))
				missing.Add(nameof(ShowDetail.Seasons));
			if (!HasCount(show.Episodes))
				missing.Add(nameof(ShowDetail.Episodes));
			if (!HasDuration(show.RuntimePerEpisode))
				missing.Add(nameof(ShowDetail.RuntimePerEpisode));
		}
		else
		{
			missing.Add("UnknownDetailType");
		}
		return missing;
	}

	private static void AddMissingCommonFields(IMediaDetail result, List<string> missing)
	{
		if (!HasText(result.Title))
			missing.Add(nameof(result.Title));
		if (!HasText(result.Tagline))
			missing.Add(nameof(result.Tagline));
		if (!HasText(result.Summary))
			missing.Add(nameof(result.Summary));
		if (!HasText(result.PosterUrl))
			missing.Add(nameof(result.PosterUrl));
		if (!HasText(result.BackdropUrl))
			missing.Add(nameof(result.BackdropUrl));
		if (!HasDate(result.ReleaseDate))
			missing.Add(nameof(result.ReleaseDate));
		if (!HasItems(result.Genres))
			missing.Add(nameof(result.Genres));
	}
}
