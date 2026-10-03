using Domain.Media;

namespace Contracts.Processing;

public sealed record MediaQueryRow(IdentifiedMedia Media)
{
	public static MediaQueryRow From(IdentifiedMedia media) => new(media);

	public MediaType MediaType => Media.Details.Type;

	public string? Title => Media.Details.Title;

	public string? Tagline => Media.Details.Tagline;

	public string? Summary => Media.Details.Summary;

	public IReadOnlyList<string> Genres => [.. Media.Details.Genres];

	public DateOnly? ReleaseDate => Media.Details.ReleaseDate is { } date && date > DateOnly.MinValue ? date : null;

	public float? Rating => Media.Details.Rating.Average;

	public int? RuntimeMinutes
	{
		get
		{
			return Media.Details switch
			{
				ShowDetails show => Minutes(show.RuntimePerEpisode),
				MovieDetails movie => Minutes(movie.Runtime),
				UnknownDetails => null,
			};
		}
	}

	public int? Seasons => Media.Details is ShowDetails show ? show.Seasons : null;

	public int? Episodes => Media.Details is ShowDetails show ? show.Episodes : null;

	private static int? Minutes(TimeSpan? runtime) => runtime is { } value ? (int)Math.Round(value.TotalMinutes) : null;
}
