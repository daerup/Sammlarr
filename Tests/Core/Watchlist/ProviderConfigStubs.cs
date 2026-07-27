using Contracts.Auth;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Media.Trending;
using Contracts.Settings;
using Domain.Providers;
using Domain.Settings;

namespace Core.Tests.Watchlist;

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
					TrendingProvider = TrendingProvider.Tmdb,
					SearchProvider = SearchProvider.Plex,
					RatingProvider = RatingProvider.Tmdb,
					AuthProvider = AuthProvider.Plex,
				}
			);
		return mock;
	}
}
