using Contracts.Media;

namespace Contracts.Auth;

public enum WatchlistProvider
{
	Plex = 0,
}

public static class WatchlistProviderExtensions
{
	extension(WatchlistProvider provider)
	{
		public MediaProvider AsMediaProvider()
		{
			return provider switch
			{
				WatchlistProvider.Plex => MediaProvider.Plex,
			};
		}
	}
}
