namespace Domain.Media;

public interface IMedia
{
	MediaIdentity Identity { get; }
	MediaType Type { get; }
	string Title { get; }
	string Tagline { get; }
	string Summary { get; }
	string PosterUrl { get; }
	string BackdropUrl { get; }
	DateOnly ReleaseDate { get; }
	ValueSet<string> Genres { get; }
	RatingSummary Rating { get; }
}

public sealed record Movie : IMedia
{
	public required MediaIdentity Identity { get; init; }
	public MediaType Type => MediaType.Movie;
	public required string Title { get; init; }
	public required string Tagline { get; init; }
	public required string Summary { get; init; }
	public required string PosterUrl { get; init; }
	public required string BackdropUrl { get; init; }
	public required DateOnly ReleaseDate { get; init; }
	public required ValueSet<string> Genres { get; init; }
	public RatingSummary Rating { get; init; } = RatingSummary.None;
	public required TimeSpan Runtime { get; init; }
}

public sealed record Show : IMedia
{
	public required MediaIdentity Identity { get; init; }
	public MediaType Type => MediaType.Show;
	public required string Title { get; init; }
	public required string Tagline { get; init; }
	public required string Summary { get; init; }
	public required string PosterUrl { get; init; }
	public required string BackdropUrl { get; init; }
	public required DateOnly ReleaseDate { get; init; }
	public required ValueSet<string> Genres { get; init; }
	public RatingSummary Rating { get; init; } = RatingSummary.None;
	public required int Seasons { get; init; }
	public required int Episodes { get; init; }
	public required TimeSpan RuntimePerEpisode { get; init; }
}
