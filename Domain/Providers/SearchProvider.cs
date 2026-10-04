using System.ComponentModel;

namespace Domain.Providers;

public enum SearchProvider
{
	[Description("TMDB")]
	Tmdb,

	[Description("Radarr + Sonarr")]
	Arr,

	[Description("Plex")]
	Plex,
}

public static class SearchProviderConversions
{
	public static IReadOnlyList<MediaProvider> Providers(this SearchProvider value)
	{
		return value switch
		{
			SearchProvider.Tmdb => [MediaProvider.Tmdb],
			SearchProvider.Plex => [MediaProvider.Plex],
			SearchProvider.Arr => [MediaProvider.Radarr, MediaProvider.Sonarr],
		};
	}
}
