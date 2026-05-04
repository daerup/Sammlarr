using Contracts.Media.Search;

namespace Contracts.Media.Metadata;

public static class MetadataCompleteness
{
	public static bool IsComplete(IMediaDetailResult result) =>
		result switch
		{
			MovieDetailResult movie => HasCommonFields(movie) && movie.Runtime > TimeSpan.Zero,
			ShowDetailResult show => HasCommonFields(show) && show.Seasons > 0 && show.Episodes > 0 && show.RuntimePerEpisode > TimeSpan.Zero,
			_ => false,
		};

	private static bool HasCommonFields(IMediaDetailResult result) =>
		!string.IsNullOrWhiteSpace(result.Title)
		&& !string.IsNullOrWhiteSpace(result.Tagline)
		&& !string.IsNullOrWhiteSpace(result.Summary)
		&& !string.IsNullOrWhiteSpace(result.PosterUrl)
		&& !string.IsNullOrWhiteSpace(result.BackdropUrl)
		&& result.ReleaseDate > DateOnly.MinValue
		&& result.Genres.Count > 0;
}
