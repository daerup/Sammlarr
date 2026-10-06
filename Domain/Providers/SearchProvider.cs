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

public static class SearchProviders
{
	extension(SearchProvider search)
	{
		public Service Service
		{
			get
			{
				return search switch
				{
					SearchProvider.Tmdb => RemoteService.Tmdb,
					SearchProvider.Plex => RemoteService.Plex,
					SearchProvider.Arr => new Arr(),
				};
			}
		}
	}
}

public static class SearchProviderConversions
{
	public static Optional<MediaProvider> Provider(this SearchProvider value)
	{
		return value switch
		{
			SearchProvider.Tmdb => Some.Of(MediaProvider.Tmdb),
			SearchProvider.Plex => Some.Of(MediaProvider.Plex),
			SearchProvider.Arr => new None(),
		};
	}
}
