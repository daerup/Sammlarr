using Domain.Media;
using Domain.Media.Metadata;

namespace Integration.Tests.Assertions;

internal static class MediaLookupReader
{
	extension(MediaLookup media)
	{
		public string Title =>
			media switch
			{
				IdentifiedMedia identified => identified.Title ?? string.Empty,
				UnmatchedMedia unmatched => unmatched.Title,
			};

		public string Summary =>
			media switch
			{
				IdentifiedMedia identified => identified.Summary ?? string.Empty,
				UnmatchedMedia unmatched => unmatched.Summary,
			};

		public string PosterUrl =>
			media switch
			{
				IdentifiedMedia identified => identified.PosterUrl ?? string.Empty,
				UnmatchedMedia unmatched => unmatched.PosterUrl,
			};

		public MediaType MediaType =>
			media switch
			{
				IdentifiedMedia identified => identified.Type,
				UnmatchedMedia unmatched => unmatched.Type,
			};

		public IReadOnlyList<string> Genres =>
			[
				.. media switch
				{
					IdentifiedMedia identified => identified.Genres,
					UnmatchedMedia unmatched => unmatched.Genres,
				},
			];

		public IReadOnlyList<MediaRating> Ratings =>
			[
				.. media switch
				{
					IdentifiedMedia identified => identified.Rating.Sources,
					UnmatchedMedia unmatched => unmatched.Rating.Sources,
				},
			];
	}
}
