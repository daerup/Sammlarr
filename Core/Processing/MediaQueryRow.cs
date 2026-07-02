using Domain.Media;

namespace Core.Processing;

public sealed record MediaQueryRow(MediaLookup Media)
{
	public static MediaQueryRow From(MediaLookup media) => new(media);

	public MediaType MediaType =>
		Media switch
		{
			IdentifiedMedia identified => identified.Type,
			UnmatchedMedia unmatched => unmatched.Type,
		};

	public string? Title =>
		Media switch
		{
			IdentifiedMedia identified => identified.Title,
			UnmatchedMedia unmatched => unmatched.Title,
		};

	public string? Tagline =>
		Media switch
		{
			IdentifiedMedia identified => identified.Tagline,
			UnmatchedMedia => null,
		};

	public string? Summary =>
		Media switch
		{
			IdentifiedMedia identified => identified.Summary,
			UnmatchedMedia unmatched => unmatched.Summary,
		};

	public IReadOnlyList<string> Genres =>
		[
			.. Media switch
			{
				IdentifiedMedia identified => identified.Genres,
				UnmatchedMedia unmatched => unmatched.Genres,
			},
		];

	public DateOnly? ReleaseDate =>
		(
			Media switch
			{
				IdentifiedMedia identified => identified.ReleaseDate,
				UnmatchedMedia unmatched => unmatched.ReleaseDate,
			}
		)
			is { } date
		&& date > DateOnly.MinValue
			? date
			: null;

	public float? Rating =>
		(
			Media switch
			{
				IdentifiedMedia identified => identified.Rating,
				UnmatchedMedia unmatched => unmatched.Rating,
			}
		).Average;

	public int? RuntimeMinutes =>
		Media switch
		{
			IdentifiedMedia identified => identified.RuntimePerEpisode is { } showRuntime ? (int)Math.Round(showRuntime.TotalMinutes)
			: identified.Runtime is { } movieRuntime ? (int)Math.Round(movieRuntime.TotalMinutes)
			: null,
			UnmatchedMedia => null,
		};

	public int? Seasons =>
		Media switch
		{
			IdentifiedMedia identified => identified.Seasons,
			UnmatchedMedia => null,
		};

	public int? Episodes =>
		Media switch
		{
			IdentifiedMedia identified => identified.Episodes,
			UnmatchedMedia => null,
		};
}
