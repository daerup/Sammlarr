using Domain.Media;
using Domain.Media.Metadata;

namespace Integration.Tests.Assertions;

internal static class MediaLookupReader
{
	extension(MediaLookup media)
	{
		public string Title => Details(media).Title ?? string.Empty;

		public string Summary => Details(media).Summary ?? string.Empty;

		public string PosterUrl => Details(media).PosterUrl ?? string.Empty;

		public MediaType MediaType => Details(media).Type;

		public IReadOnlyList<string> Genres => [.. Details(media).Genres];

		public IReadOnlyList<MediaRating> Ratings => [.. Details(media).Rating.Sources];
	}

	private static MediaDetails Details(MediaLookup media)
	{
		return media switch
		{
			IdentifiedMedia identified => identified.Details,
			UnmatchedMedia unmatched => unmatched.Details,
		};
	}
}
