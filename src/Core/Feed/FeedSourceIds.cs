using Domain.Providers;

namespace Core.Feed;

internal static class FeedSourceIds
{
	internal static IReadOnlySet<MediaProvider> Of(FeedProvider provider)
	{
		HashSet<MediaProvider> sources = provider switch
		{
			FeedProvider.Mdblist => [MediaProvider.Mdblist, MediaProvider.Tmdb, MediaProvider.Imdb, MediaProvider.Tvdb, MediaProvider.Trakt, MediaProvider.Mal],
			FeedProvider.Tmdb => [MediaProvider.Tmdb],
			FeedProvider.AniList => [MediaProvider.AniList, MediaProvider.Mal],
			FeedProvider.Mal => [MediaProvider.Mal],
			FeedProvider.Trakt => [MediaProvider.Trakt, MediaProvider.Imdb, MediaProvider.Tmdb, MediaProvider.Tvdb],
		};
		return sources;
	}
}
