namespace Contracts.Media.Blacklist;

public interface IBlacklistItem
{
	IMediaId SourceId { get; }
	string Title { get; }
	MediaType MediaType { get; }
	string Description { get; }
	string ArtUrl { get; }
	string PosterUrl { get; }
	DateTimeOffset ReleaseDate { get; }
	DateTimeOffset BlacklistDate { get; }
}
