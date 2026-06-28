namespace UI.Shared.Media;

public static class MediaPresentation
{
	extension(MediaMatch media)
	{
		public MediaType MediaType =>
			media switch
			{
				IMedia resolved => resolved.Type,
				IPartialMedia partial => partial.Type,
				UnmatchedMedia unmatched => unmatched.Type,
			};

		public MediaId Id =>
			media switch
			{
				IMedia resolved => resolved.Identity.Id,
				IPartialMedia partial => partial.Identity.Id,
				_ => throw new InvalidOperationException(),
			};

		public string Key =>
			media.IsUnresolved ? string.Join("|", media.ExternalIds.Select(id => $"{id.Source}:{id.Value}").OrderBy(value => value)) : media.Id.Value.ToString();

		public string Title =>
			media switch
			{
				IMedia resolved => resolved.Title,
				IPartialMedia partial => partial.Title ?? "",
				UnmatchedMedia unmatched => unmatched.Title,
			};

		public string Tagline =>
			media switch
			{
				IMedia resolved => resolved.Tagline,
				IPartialMedia partial => partial.Tagline ?? "",
				_ => "",
			};

		public string Summary =>
			media switch
			{
				IMedia resolved => resolved.Summary,
				IPartialMedia partial => partial.Summary ?? "",
				UnmatchedMedia unmatched => unmatched.Summary,
			};

		public string PosterUrl =>
			media switch
			{
				IMedia resolved => resolved.PosterUrl,
				IPartialMedia partial => partial.PosterUrl ?? "",
				UnmatchedMedia unmatched => unmatched.PosterUrl,
			};

		public string BackdropUrl =>
			media switch
			{
				IMedia resolved => resolved.BackdropUrl,
				IPartialMedia partial => partial.BackdropUrl ?? "",
				UnmatchedMedia unmatched => unmatched.BackdropUrl,
			};

		public DateOnly ReleaseDate =>
			media switch
			{
				IMedia resolved => resolved.ReleaseDate,
				IPartialMedia partial => partial.ReleaseDate ?? DateOnly.MinValue,
				UnmatchedMedia unmatched => unmatched.ReleaseDate ?? DateOnly.MinValue,
			};

		public float? Rating =>
			(
				media switch
				{
					IMedia resolved => resolved.Rating,
					IPartialMedia partial => partial.Rating,
					UnmatchedMedia unmatched => unmatched.Rating,
				}
			).Average;

		public IReadOnlyList<string> Genres =>
			media switch
			{
				IMedia resolved => [.. resolved.Genres],
				IPartialMedia partial => [.. partial.Genres],
				UnmatchedMedia unmatched => [.. unmatched.Genres],
			};

		public IReadOnlyList<IExternalMediaId> ExternalIds => [.. media.Externals];

		public bool IsUnresolved => media is UnmatchedMedia;

		public int? Seasons =>
			media switch
			{
				Show show => show.Seasons,
				PartialShow show => show.Seasons,
				_ => null,
			};

		public int? Episodes =>
			media switch
			{
				Show show => show.Episodes,
				PartialShow show => show.Episodes,
				_ => null,
			};

		public TimeSpan? Runtime =>
			media switch
			{
				Movie movie => movie.Runtime,
				PartialMovie movie => movie.Runtime,
				_ => null,
			};
	}
}
