namespace Contracts.Media.Trending;

public record UnresolvedTrendingMedia : ITrendingMedia
{
	public required IMediaId SourceId { get; init; }
	public required MediaType MediaType { get; init; }
	public required string Title { get; init; }
	public required string Description { get; init; }
	public required string PosterUrl { get; init; }
	public required string BackdropUrl { get; init; }
	public required float Rating { get; init; }
	public required DateTimeOffset ReleaseDate { get; init; }
}
