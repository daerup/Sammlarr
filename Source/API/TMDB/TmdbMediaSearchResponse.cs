using Contracts.Media;
using Contracts.Media.Trending;

namespace API.TMDB;

internal record TmdbTrendingMedia : ITrendingMedia
{
	public required IMediaId SourceId { get; init; }
	public required IMediaId WatchlistProviderId { get; init; }
	public required MediaType MediaType { get; init; }
	public required string Title { get; init; }
	public required string Description { get; init; }
	public required string PosterUrl { get; init; }
	public required string BackdropUrl { get; init; }
	public required float Rating { get; init; }
	public required DateTimeOffset ReleaseDate { get; init; }
}
