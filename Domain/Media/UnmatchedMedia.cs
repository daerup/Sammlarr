namespace Domain.Media;

public sealed record UnmatchedMedia
{
	public required ValueSet<IExternalMediaId> Externals { get; init; }
	public string Title { get; init; } = "";
	public string Summary { get; init; } = "";
	public string PosterUrl { get; init; } = "";
	public string BackdropUrl { get; init; } = "";
	public DateOnly? ReleaseDate { get; init; }
	public ValueSet<string> Genres { get; init; }
	public RatingSummary Rating { get; init; } = RatingSummary.None;
	public MediaType Type => Externals.FirstOrDefault()?.Type ?? MediaType.Unknown;
}
