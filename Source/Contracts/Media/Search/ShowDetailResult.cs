using Contracts.Media.Metadata;

namespace Contracts.Media.Search;

public record ShowDetailResult : IMediaDetailResult
{
	public required int Seasons { get; init; }
	public required int Episodes { get; init; }
	public MediaType MediaType => MediaType.Show;
	public required MediaId Id { get; init; }
	public required IReadOnlyList<IExternalMediaId> Externals { get; init; }
	public required string PosterUrl { get; init; }
	public required string BackdropUrl { get; init; }
	public required string Title { get; init; }
	public required string Tagline { get; init; }
	public required string Summary { get; init; }
	public required TimeSpan RuntimePerEpisode { get; init; }
	public required DateOnly ReleaseDate { get; init; }
	public required IReadOnlyList<string> Genres { get; init; }
	public IReadOnlyList<MediaRating> Ratings { get; init; } = [];
	public float? Rating => Ratings.Count > 0 ? Ratings[0].NormalizedValue : null;
	public string MediaTypeText => MediaType.ToString();
	public int? RuntimeMinutes => RuntimePerEpisode.TotalMinutes > 0 ? (int)RuntimePerEpisode.TotalMinutes : null;
	public int? SeasonCount => Seasons;
	public int? EpisodeCount => Episodes;
}
