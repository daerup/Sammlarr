namespace Contracts.Media.Search;

public record ShowDetailResult : IMediaDetailResult
{
	public required int Seasons { get; init; }
	public required int Episodes { get; init; }
	public MediaType MediaType => MediaType.Show;
	public required IMediaId SourceId { get; init; }
	public required IMediaId[] AdditionalIds { get; init; }
	public required string PosterUrl { get; init; }
	public required string BackdropUrl { get; init; }
	public required string Title { get; init; }
	public required string Tagline { get; init; }
	public required string Summary { get; init; }
	public required TimeSpan RuntimePerEpisode { get; init; }
	public required DateOnly ReleaseDate { get; init; }
	public required IReadOnlyList<string> Genres { get; init; }
}
