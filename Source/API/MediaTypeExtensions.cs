using Contracts.Media;

namespace API;

internal static class MediaTypeExtensions
{
	extension(MediaType)
	{
		public static MediaType FromString(string mediaType)
		{
			return mediaType.ToLowerInvariant() switch
			{
				"movie" or "movies" => MediaType.Movie,
				"show" or "series" or "tv" => MediaType.Show,
			};
		}
	}
}
