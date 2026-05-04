using Contracts.Media.Search;

namespace Contracts.Media.Metadata;

public static class MetadataMerger
{
	public static IMediaDetailResult FillMissing(IMediaDetailResult primary, IMediaDetailResult secondary) =>
		(primary, secondary) switch
		{
			(MovieDetailResult p, MovieDetailResult s) => MergeMovie(p, s),
			(ShowDetailResult p, ShowDetailResult s) => MergeShow(p, s),
			_ => primary,
		};

	private static MovieDetailResult MergeMovie(MovieDetailResult primary, MovieDetailResult secondary) =>
		primary with
		{
			Title = string.IsNullOrWhiteSpace(primary.Title) ? secondary.Title : primary.Title,
			Tagline = string.IsNullOrWhiteSpace(primary.Tagline) ? secondary.Tagline : primary.Tagline,
			Summary = string.IsNullOrWhiteSpace(primary.Summary) ? secondary.Summary : primary.Summary,
			PosterUrl = string.IsNullOrWhiteSpace(primary.PosterUrl) ? secondary.PosterUrl : primary.PosterUrl,
			BackdropUrl = string.IsNullOrWhiteSpace(primary.BackdropUrl) ? secondary.BackdropUrl : primary.BackdropUrl,
			ReleaseDate = primary.ReleaseDate > DateOnly.MinValue ? primary.ReleaseDate : secondary.ReleaseDate,
			Genres = primary.Genres.Count > 0 ? primary.Genres : secondary.Genres,
			Runtime = primary.Runtime > TimeSpan.Zero ? primary.Runtime : secondary.Runtime,
		};

	private static ShowDetailResult MergeShow(ShowDetailResult primary, ShowDetailResult secondary) =>
		primary with
		{
			Title = string.IsNullOrWhiteSpace(primary.Title) ? secondary.Title : primary.Title,
			Tagline = string.IsNullOrWhiteSpace(primary.Tagline) ? secondary.Tagline : primary.Tagline,
			Summary = string.IsNullOrWhiteSpace(primary.Summary) ? secondary.Summary : primary.Summary,
			PosterUrl = string.IsNullOrWhiteSpace(primary.PosterUrl) ? secondary.PosterUrl : primary.PosterUrl,
			BackdropUrl = string.IsNullOrWhiteSpace(primary.BackdropUrl) ? secondary.BackdropUrl : primary.BackdropUrl,
			ReleaseDate = primary.ReleaseDate > DateOnly.MinValue ? primary.ReleaseDate : secondary.ReleaseDate,
			Genres = primary.Genres.Count > 0 ? primary.Genres : secondary.Genres,
			Seasons = primary.Seasons > 0 ? primary.Seasons : secondary.Seasons,
			Episodes = primary.Episodes > 0 ? primary.Episodes : secondary.Episodes,
			RuntimePerEpisode = primary.RuntimePerEpisode > TimeSpan.Zero ? primary.RuntimePerEpisode : secondary.RuntimePerEpisode,
		};
}
