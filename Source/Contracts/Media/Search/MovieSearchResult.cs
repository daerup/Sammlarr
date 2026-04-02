namespace Contracts.Media.Search;

public record MovieSearchResult : IMediaSearchResult
{
	public MediaType MediaType => MediaType.Movie;
	public required IMediaId SourceId { get; init; }
	public required int? Year { get; init; }
	public required string Title { get; init; }
	public required string? PosterUrl { get; init; }
	public required string? BackdropUrl { get; init; }
	public required TimeSpan? Runtime { get; init; }
	public required DateOnly? ReleaseDate { get; init; }
	public required IReadOnlyList<string> Genres { get; init; }
}
