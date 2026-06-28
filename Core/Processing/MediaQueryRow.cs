namespace Core.Processing;

public sealed record MediaQueryRow(MediaMatch Media)
{
	public static MediaQueryRow From(MediaMatch media) => new(media);

	public MediaType MediaType =>
		Media switch
		{
			IMedia resolved => resolved.Type,
			IPartialMedia partial => partial.Type,
			UnmatchedMedia unmatched => unmatched.Type,
		};

	public string? Title =>
		Media switch
		{
			IMedia resolved => resolved.Title,
			IPartialMedia partial => partial.Title,
			UnmatchedMedia unmatched => unmatched.Title,
		};

	public string? Tagline =>
		Media switch
		{
			IMedia resolved => resolved.Tagline,
			IPartialMedia partial => partial.Tagline,
			_ => null,
		};

	public string? Summary =>
		Media switch
		{
			IMedia resolved => resolved.Summary,
			IPartialMedia partial => partial.Summary,
			UnmatchedMedia unmatched => unmatched.Summary,
		};

	public IReadOnlyList<string> Genres =>
		[
			.. Media switch
			{
				IMedia resolved => resolved.Genres,
				IPartialMedia partial => partial.Genres,
				UnmatchedMedia unmatched => unmatched.Genres,
			},
		];

	public DateOnly? ReleaseDate =>
		Media switch
		{
			IMedia resolved => resolved.ReleaseDate,
			IPartialMedia partial => partial.ReleaseDate,
			UnmatchedMedia unmatched => unmatched.ReleaseDate,
		}
			is { } date
		&& date > DateOnly.MinValue
			? date
			: null;

	public float? Rating =>
		(
			Media switch
			{
				IMedia resolved => resolved.Rating,
				IPartialMedia partial => partial.Rating,
				UnmatchedMedia unmatched => unmatched.Rating,
			}
		).Average;

	public int? RuntimeMinutes =>
		Media switch
		{
			Movie movie => (int)Math.Round(movie.Runtime.TotalMinutes),
			Show show => (int)Math.Round(show.RuntimePerEpisode.TotalMinutes),
			PartialMovie movie => movie.Runtime is { } runtime ? (int)Math.Round(runtime.TotalMinutes) : null,
			PartialShow show => show.RuntimePerEpisode is { } runtime ? (int)Math.Round(runtime.TotalMinutes) : null,
			_ => null,
		};

	public int? Seasons =>
		Media switch
		{
			Show show => show.Seasons,
			PartialShow show => show.Seasons,
			_ => null,
		};

	public int? Episodes =>
		Media switch
		{
			Show show => show.Episodes,
			PartialShow show => show.Episodes,
			_ => null,
		};
}
