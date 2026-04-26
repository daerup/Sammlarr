namespace Contracts.Media.Search;

public record UnresolvedMediaResult : IMediaDetailResult
{
    public required IMediaId SourceId { get; init; }
    public required IMediaId[] AdditionalIds { get; init; }
    public required MediaType MediaType { get; init; }
    public string? PosterUrl => null;
    public string? BackdropUrl => null;
    public required string Title { get; init; }
    public string Summary => string.Empty;
    public TimeSpan? Runtime => null;
    public required DateOnly? ReleaseDate { get; init; }
    public IReadOnlyList<string> Genres => [];
}
