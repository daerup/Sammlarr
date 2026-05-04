namespace Contracts.Media.Search;

public interface IMediaDetailResult
{
	public IMediaId SourceId { get; }
	public IMediaId[] AdditionalIds { get; }
	public MediaType MediaType { get; }

	public string PosterUrl { get; }
	public string BackdropUrl { get; }

	public string Title { get; }
	public string Tagline { get; }
	public string Summary { get; }

	public DateOnly ReleaseDate { get; }

	public IReadOnlyList<string> Genres { get; }
}
