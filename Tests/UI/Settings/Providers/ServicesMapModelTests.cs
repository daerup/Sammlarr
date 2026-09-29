using Domain.Providers;
using Domain.Settings;
using UI.Features.Settings.Components.Providers;

namespace UI.Tests.Settings.Providers;

public class ServicesMapModelTests
{
	private static readonly ProviderConfig Config = new()
	{
		WatchlistProvider = WatchlistProvider.Plex,
		TrendingProvider = TrendingProvider.Tmdb,
		SearchProvider = SearchProvider.Plex,
		RatingProvider = RatingProvider.Tmdb,
		AuthProvider = AuthProvider.Plex,
		RegistrationMode = RegistrationMode.Open,
	};

	[Test]
	public async Task Lines_ForTodaysProviders_RunFromEveryRoleToItsServiceWithFeedsMarkedPerFeed()
	{
		var lines = ServicesMapModel.For(Config).Lines.Select(line => line.PerFeed ? $"{line.Provider} per feed" : $"{line.Provider}");

		await Assert.That(lines).IsEquivalentTo(["Plex", "Plex", "Plex", "Tmdb", "Tmdb", "Tmdb per feed", "Mdblist per feed"]);
	}

	[Test]
	public async Task Services_SharedOnesSitBesideTheirRolesAndFeedOnlyOnesStackBelowAsSources()
	{
		var services = ServicesMapModel
			.For(Config)
			.Services.Select(service => service.IsFeedSource ? $"{service.Provider} {service.Y} source" : $"{service.Provider} {service.Y}");

		await Assert.That(services).IsEquivalentTo(["Plex 72", "Tmdb 192", "Mdblist 259 source"]);
	}
}
