namespace Contracts.Media.Trending;

public interface ITrendingMedia
{
	IMediaId SourceId { get; }
	IMediaId WatchlistProviderId { get; }
	MediaProvider Provider { get; }
	MediaType MediaType { get; }
	string Title { get; }
	string Description { get; }
	string PosterUrl { get; }
	string BackdropUrl { get; }
	float Rating { get; }
	DateTimeOffset ReleaseDate { get; }
}
