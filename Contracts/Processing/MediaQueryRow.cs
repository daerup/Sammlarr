using Domain.Media;

namespace Contracts.Processing;

public sealed record MediaQueryRow(IdentifiedMedia Media)
{
	public static MediaQueryRow From(IdentifiedMedia media) => new(media);

	public MediaType MediaType => Media.Type;

	public string? Title => Media.Title;

	public string? Tagline => Media.Tagline;

	public string? Summary => Media.Summary;

	public IReadOnlyList<string> Genres => [.. Media.Genres];

	public DateOnly? ReleaseDate => Media.ReleaseDate is { } date && date > DateOnly.MinValue ? date : null;

	public float? Rating => Media.Rating.Average;

	public int? RuntimeMinutes =>
		Media.RuntimePerEpisode is { } showRuntime ? (int)Math.Round(showRuntime.TotalMinutes)
		: Media.Runtime is { } movieRuntime ? (int)Math.Round(movieRuntime.TotalMinutes)
		: null;

	public int? Seasons => Media.Seasons;

	public int? Episodes => Media.Episodes;
}
