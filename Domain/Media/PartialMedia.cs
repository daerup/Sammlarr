using Domain.Media.Metadata;

namespace Domain.Media;

public interface IPartialMedia
{
	MediaIdentity Identity { get; }
	MediaType Type { get; }
	string? Title { get; }
	string? Tagline { get; }
	string? Summary { get; }
	string? PosterUrl { get; }
	string? BackdropUrl { get; }
	DateOnly? ReleaseDate { get; }
	ValueSet<string> Genres { get; }
	RatingSummary Rating { get; }
	MediaField ConfirmedAbsent { get; }
}

public sealed record PartialMovie : IPartialMedia
{
	public required MediaIdentity Identity { get; init; }
	public MediaType Type => MediaType.Movie;
	public string? Title { get; init; }
	public string? Tagline { get; init; }
	public string? Summary { get; init; }
	public string? PosterUrl { get; init; }
	public string? BackdropUrl { get; init; }
	public DateOnly? ReleaseDate { get; init; }
	public ValueSet<string> Genres { get; init; }
	public RatingSummary Rating { get; init; }
	public MediaField ConfirmedAbsent { get; init; }
	public TimeSpan? Runtime { get; init; }
}

public sealed record PartialShow : IPartialMedia
{
	public required MediaIdentity Identity { get; init; }
	public MediaType Type => MediaType.Show;
	public string? Title { get; init; }
	public string? Tagline { get; init; }
	public string? Summary { get; init; }
	public string? PosterUrl { get; init; }
	public string? BackdropUrl { get; init; }
	public DateOnly? ReleaseDate { get; init; }
	public ValueSet<string> Genres { get; init; }
	public RatingSummary Rating { get; init; }
	public MediaField ConfirmedAbsent { get; init; }
	public int? Seasons { get; init; }
	public int? Episodes { get; init; }
	public TimeSpan? RuntimePerEpisode { get; init; }
}
