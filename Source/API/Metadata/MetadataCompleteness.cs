using Contracts.Media.Search;
using static API.Metadata.MetadataFields;

namespace API.Metadata;

internal static class MetadataCompleteness
{
	public static IReadOnlyList<string> GetMissingFields(IMediaDetailResult result)
	{
		var missing = new List<string>();
		switch (result)
		{
			case MovieDetailResult movie:
				AddMissingCommonFields(movie, missing);
				if (!HasDuration(movie.Runtime))
					missing.Add(nameof(MovieDetailResult.Runtime));
				break;
			case ShowDetailResult show:
				AddMissingCommonFields(show, missing);
				if (!HasCount(show.Seasons))
					missing.Add(nameof(ShowDetailResult.Seasons));
				if (!HasCount(show.Episodes))
					missing.Add(nameof(ShowDetailResult.Episodes));
				if (!HasDuration(show.RuntimePerEpisode))
					missing.Add(nameof(ShowDetailResult.RuntimePerEpisode));
				break;
			default:
				missing.Add("UnknownDetailType");
				break;
		}
		return missing;
	}

	private static void AddMissingCommonFields(IMediaDetailResult result, List<string> missing)
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
