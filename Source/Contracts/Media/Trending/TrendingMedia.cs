namespace Contracts.Media.Trending;

public record TrendingMedia : ITrendingMedia
{
	public required MediaId Id { get; init; }
	public required IReadOnlyList<IExternalMediaId> Externals { get; init; }
	public required MediaType MediaType { get; init; }
	public required string Title { get; init; }
	public required string Description { get; init; }
	public required string PosterUrl { get; init; }
	public required string BackdropUrl { get; init; }
	public required float Rating { get; init; }
	public required DateTimeOffset ReleaseDate { get; init; }
}
