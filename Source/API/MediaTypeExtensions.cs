using Contracts.Media;

namespace API;

internal static class MediaTypeExtensions
{
	extension(MediaType)
	{
		public static MediaType Parse(string mediaType)
		{
			MediaType.TryParse(mediaType, out MediaType result);
			return result;
		}

		public static bool TryParse(string mediaType, out MediaType result)
		{
			MediaType? parsed = mediaType.ToLowerInvariant() switch
			{
				"movie" or "movies" => MediaType.Movie,
				"show" or "series" or "tv" => MediaType.Show,
				_ => null,
			};

			result = parsed ?? default;
			return parsed is not null;
		}
	}
}
