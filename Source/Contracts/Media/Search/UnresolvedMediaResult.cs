using Contracts.Media.Metadata;

namespace Contracts.Media.Search;

public record UnresolvedMediaResult : IMediaDetailResult
{
	public required MediaId Id { get; init; }
	public required IReadOnlyList<IExternalMediaId> Externals { get; init; }
	public required MediaType MediaType { get; init; }
	public string PosterUrl => string.Empty;
	public string BackdropUrl => string.Empty;
	public required string Title { get; init; }
	public string Tagline => string.Empty;
	public string Summary => string.Empty;
	public required DateOnly ReleaseDate { get; init; }
	public IReadOnlyList<string> Genres => [];
	public IReadOnlyList<MediaRating> Ratings => [];
	public float? Rating => null;
}
