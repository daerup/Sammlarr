using Contracts.Media;
using Domain.Media;

namespace API;

internal static class MediaTypeExtensions
{
	extension(MediaType)
	{
		public static MediaType Parse(string mediaType)
		{
			TryParse(mediaType, out MediaType result);
			return result;
		}

		public static bool TryParse(string mediaType, out MediaType result)
		{
			result = mediaType.ToLowerInvariant() switch
			{
				"movie" or "movies" => MediaType.Movie,
				"show" or "series" or "tv" => MediaType.Show,
				_ => MediaType.Unknown,
			};

			return result is not MediaType.Unknown;
		}
	}
}
