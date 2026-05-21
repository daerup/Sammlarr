using Contracts.Auth;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Media.Trending;
using Contracts.Settings;

namespace Core.Tests.Unit.Watchlist;

internal static class ProviderConfigStubs
{
	public static Mock<IProviderConfigRepository> PlexWatchlist()
	{
		var mock = IProviderConfigRepository.Mock();
		mock.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					WatchlistProvider = WatchlistProvider.Plex,
					TrendingProvider = MediaRecommendationProvider.Tmdb,
					SearchProvider = MediaSearchProvider.Plex,
					RatingProvider = RatingProvider.Tmdb,
				}
			);
		return mock;
	}
}
