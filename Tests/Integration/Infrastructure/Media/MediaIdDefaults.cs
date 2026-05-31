using Contracts.Media;

namespace Integration.Tests.Infrastructure.Media;

public static class MediaIdDefaults
{
	public static IReadOnlyDictionary<MediaProvider, string> ForOrdinal(int ordinal) =>
		new Dictionary<MediaProvider, string> { [MediaProvider.Tmdb] = ordinal.ToString(), [MediaProvider.Plex] = $"plex-{ordinal}" };

	public const int DefaultYear = 2024;
	public const string DefaultReleaseDate = "2024-01-15";
}
