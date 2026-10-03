using Domain.Media;

namespace UI.Shared.Media;

public static class MediaPresentation
{
	extension(MediaLookup media)
	{
		public MediaType MediaType =>
			media switch
			{
				IdentifiedMedia identified => identified.Type,
				UnmatchedMedia unmatched => unmatched.Details.Type,
			};

		public MediaId Id =>
			media switch
			{
				IdentifiedMedia identified => identified.Identity.Id,
				_ => throw new InvalidOperationException(),
			};

		public string Key =>
			media.IsUnmatched ? string.Join("|", media.ExternalIds.Select(id => $"{id.Source}:{id.Value}").OrderBy(value => value)) : media.Id.Value.ToString();

		public string Title =>
			media switch
			{
				IdentifiedMedia identified => identified.Title ?? "",
				UnmatchedMedia unmatched => unmatched.Details.Title ?? "",
			};

		public string Tagline =>
			media switch
			{
				IdentifiedMedia identified => identified.Tagline ?? "",
				UnmatchedMedia => "",
			};

		public string Summary =>
			media switch
			{
				IdentifiedMedia identified => identified.Summary ?? "",
				UnmatchedMedia unmatched => unmatched.Details.Summary ?? "",
			};

		public string PosterUrl =>
			media switch
			{
				IdentifiedMedia identified => identified.PosterUrl ?? "",
				UnmatchedMedia unmatched => unmatched.Details.PosterUrl ?? "",
			};

		public string BackdropUrl =>
			media switch
			{
				IdentifiedMedia identified => identified.BackdropUrl ?? "",
				UnmatchedMedia unmatched => unmatched.Details.BackdropUrl ?? "",
			};

		public DateOnly ReleaseDate =>
			media switch
			{
				IdentifiedMedia identified => identified.ReleaseDate ?? DateOnly.MinValue,
				UnmatchedMedia unmatched => unmatched.Details.ReleaseDate ?? DateOnly.MinValue,
			};

		public float? Rating =>
			(
				media switch
				{
					IdentifiedMedia identified => identified.Rating,
					UnmatchedMedia unmatched => unmatched.Details.Rating,
				}
			).Average;

		public IReadOnlyList<string> Genres =>
			media switch
			{
				IdentifiedMedia identified => [.. identified.Genres],
				UnmatchedMedia unmatched => [.. unmatched.Details.Genres],
			};

		public IReadOnlyList<IExternalMediaId> ExternalIds => [.. media.Externals];

		public int? Seasons =>
			media switch
			{
				IdentifiedMedia identified => identified.Seasons,
				UnmatchedMedia => null,
			};

		public int? Episodes =>
			media switch
			{
				IdentifiedMedia identified => identified.Episodes,
				UnmatchedMedia => null,
			};

		public TimeSpan? Runtime =>
			media switch
			{
				IdentifiedMedia identified => identified.Runtime,
				UnmatchedMedia => null,
			};
	}
}
