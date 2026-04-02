namespace Contracts.Media;

public record MediaSearchResult(IMediaId SourceId, string Title)
{
	public MediaType MediaType { get; init; }

	public int? Year { get; init; }

	public string? PosterUrl { get; init; }

	public string? Description { get; init; }

	public string? Tagline { get; init; }

	public double? Rating { get; init; }

	public double? AudienceRating { get; init; }

	public TimeSpan? Runtime { get; init; }

	public DateOnly? ReleaseDate { get; init; }

	public IReadOnlyList<string> Genres { get; init; } = [];
}
