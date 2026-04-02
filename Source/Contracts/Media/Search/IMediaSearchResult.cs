namespace Contracts.Media.Search;

public interface IMediaSearchResult
{
	public IMediaId SourceId { get; }
	public MediaType MediaType { get; }

	public string? PosterUrl { get; }
	public string? BackdropUrl { get; }

	public string Title { get; }

	public TimeSpan? Runtime { get; }

	public DateOnly? ReleaseDate { get; }

	public IReadOnlyList<string> Genres { get; }
}
