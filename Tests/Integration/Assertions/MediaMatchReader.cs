namespace Integration.Tests.Assertions;

internal static class MediaMatchReader
{
	extension(MediaMatch media)
	{
		public string Title =>
			media switch
			{
				IMedia resolved => resolved.Title,
				IPartialMedia partial => partial.Title ?? string.Empty,
				UnmatchedMedia unmatched => unmatched.Title,
			};

		public string Summary =>
			media switch
			{
				IMedia resolved => resolved.Summary,
				IPartialMedia partial => partial.Summary ?? string.Empty,
				UnmatchedMedia unmatched => unmatched.Summary,
			};

		public string PosterUrl =>
			media switch
			{
				IMedia resolved => resolved.PosterUrl,
				IPartialMedia partial => partial.PosterUrl ?? string.Empty,
				UnmatchedMedia unmatched => unmatched.PosterUrl,
			};

		public MediaType MediaType =>
			media switch
			{
				IMedia resolved => resolved.Type,
				IPartialMedia partial => partial.Type,
				UnmatchedMedia unmatched => unmatched.Type,
			};

		public IReadOnlyList<string> Genres =>
			[
				.. media switch
				{
					IMedia resolved => resolved.Genres,
					IPartialMedia partial => partial.Genres,
					UnmatchedMedia unmatched => unmatched.Genres,
				},
			];

		public IReadOnlyList<MediaRating> Ratings =>
			[
				.. media switch
				{
					IMedia resolved => resolved.Rating.Sources,
					IPartialMedia partial => partial.Rating.Sources,
					UnmatchedMedia unmatched => unmatched.Rating.Sources,
				},
			];
	}
}
