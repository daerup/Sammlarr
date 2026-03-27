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
				"movie" => MediaType.Movie,
				"show" or "series" or "tv" => MediaType.Show,
				_ => MediaType.Unknown,
			};
		}
	}
}
