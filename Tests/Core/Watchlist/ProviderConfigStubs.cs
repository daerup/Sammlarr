using Contracts.Settings;
using Domain.Providers;
using Domain.Settings;

namespace Core.Tests.Watchlist;

internal static class ProviderConfigStubs
{
	public static Mock<IProviderConfigService> PlexWatchlist()
	{
		var mock = IProviderConfigService.Mock();
		mock.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					WatchlistProvider = WatchlistProvider.Plex,
					TrendingProvider = TrendingProvider.Tmdb,
					SearchProvider = SearchProvider.Plex,
					RatingProvider = RatingProvider.Tmdb,
					AuthProvider = AuthProvider.Plex,
					RegistrationMode = RegistrationMode.Open,
				}
			);
		return mock;
	}
}
