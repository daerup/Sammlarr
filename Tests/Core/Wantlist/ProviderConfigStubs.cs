using Contracts.Settings;
using Domain.Providers;
using Domain.Settings;

namespace Core.Tests.Wantlist;

internal static class ProviderConfigStubs
{
	public static Mock<IProviderConfigService> PlexWantlist()
	{
		var mock = IProviderConfigService.Mock();
		mock.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					Sink = SinkKind.Plex,
					TrendingProvider = TrendingProvider.Tmdb,
					SearchProvider = SearchProvider.Plex,
					AuthProvider = AuthProvider.Plex,
					RegistrationMode = RegistrationMode.Open,
				}
			);
		return mock;
	}
}
