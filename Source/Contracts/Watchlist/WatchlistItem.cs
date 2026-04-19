using Contracts.Media;

namespace Contracts.Watchlist;

public interface IWatchlistItem
{
	IMediaId SourceId { get; }
	string Title { get; }
	MediaType MediaType { get; }
	string Tagline { get; }
	TmdbId[] TmdbId { get; }
	ImdbId[] ImdbId { get; }
	TvdbId[] TvdbId { get; }
	string ArtUrl { get; }
	string PosterUrl { get; }
	DateTimeOffset ReleaseDate { get; }
	DateTimeOffset AddedDate { get; }
}
