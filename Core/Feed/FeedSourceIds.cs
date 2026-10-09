using Domain.Providers;

namespace Core.Feed;

internal static class FeedSourceIds
{
	internal static IReadOnlySet<MediaProvider> Of(FeedProvider provider)
	{
		return provider switch
		{
			FeedProvider.Mdblist => new HashSet<MediaProvider>
			{
				MediaProvider.Mdblist,
				MediaProvider.Tmdb,
				MediaProvider.Imdb,
				MediaProvider.Tvdb,
				MediaProvider.Trakt,
				MediaProvider.Mal,
			},
			FeedProvider.Tmdb => new HashSet<MediaProvider> { MediaProvider.Tmdb },
			FeedProvider.AniList => new HashSet<MediaProvider> { MediaProvider.AniList, MediaProvider.Mal },
			FeedProvider.Mal => new HashSet<MediaProvider> { MediaProvider.Mal },
			FeedProvider.Trakt => new HashSet<MediaProvider> { MediaProvider.Trakt, MediaProvider.Imdb, MediaProvider.Tmdb, MediaProvider.Tvdb },
		};
	}
}
