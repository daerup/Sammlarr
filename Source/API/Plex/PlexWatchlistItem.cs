using Contracts.Media;
using Contracts.Watchlist;

namespace API.Plex;

public sealed class PlexWatchlistItem : IWatchlistItem
{
	public IMediaId SourceId => RatingKey;
	public required PlexId RatingKey { get; set; }
	public required string Title { get; init; }

	public required MediaType MediaType { get; init; }
	public required string Description { get; init; }
	public required string ArtUrl { get; init; }
	public required string PosterUrl { get; init; }
	public required DateTimeOffset AddedDate { get; init; }
	public required DateTimeOffset ReleaseDate { get; init; }
	public required TmdbId[] TmdbId { get; init; }
	public required ImdbId[] ImdbId { get; init; }
	public required TvdbId[] TvdbId { get; init; }
}
