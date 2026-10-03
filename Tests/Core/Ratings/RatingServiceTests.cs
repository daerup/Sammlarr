using Contracts.Auth;
using Contracts.Ratings;
using Core.Ratings;
using Domain.Media;
using Domain.Providers;

namespace Core.Tests.Ratings;

public class RatingServiceTests
{
	private readonly Mock<IRatingDefaultRepository> _defaults = IRatingDefaultRepository.Mock();
	private readonly Mock<IApiKeyService> _keys = IApiKeyService.Mock();
	private readonly Mock<ICredentialService> _credentials = ICredentialService.Mock();

	public RatingServiceTests()
	{
		_keys.IsStoredAsync(Any<ApiKeyProvider>(), Any()).Returns(false);
		_credentials.IsStoredAsync(Any<AuthProvider>(), Any()).Returns(false);
	}

	private RatingService Testee => new(_defaults.Object, _keys.Object, _credentials.Object);

	[Test]
	public async Task GetAvailableAsync_WithOnlyATmdbKey_OffersOnlyTmdb()
	{
		_keys.IsStoredAsync(ApiKeyProvider.Tmdb, Any()).Returns(true);

		await Assert.That(await Testee.GetAvailableAsync()).IsEquivalentTo([RatingSource.Tmdb]);
	}

	[Test]
	public async Task GetAvailableAsync_WithAPlexAccount_OffersEverySourcePlexSupplies()
	{
		_credentials.IsStoredAsync(AuthProvider.Plex, Any()).Returns(true);

		await Assert
			.That(await Testee.GetAvailableAsync())
			.IsEquivalentTo([RatingSource.Tmdb, RatingSource.Imdb, RatingSource.RottenTomatoesCritics, RatingSource.RottenTomatoesAudience]);
	}

	[Test]
	public async Task GetAvailableAsync_WithNothingConnected_OffersNoSource()
	{
		await Assert.That(await Testee.GetAvailableAsync()).IsEmpty();
	}

	[Test]
	public async Task GetAvailableAsync_NeverLooksUpAProviderThatSuppliesNoRating()
	{
		await Testee.GetAvailableAsync();

		_keys.IsStoredAsync(ApiKeyProvider.Mdblist, Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task GetDefaultAsync_WhenNothingIsStored_AveragesEverySource()
	{
		_defaults.FindAsync(Any()).Returns(new None());

		await Assert.That(await Testee.GetDefaultAsync()).IsEqualTo(new RatingStrategy(AveragedRating.Everything));
	}

	[Test]
	public async Task GetDefaultAsync_WhenAStrategyIsStored_ReturnsIt()
	{
		var stored = new RatingStrategy(new OrderedRating([RatingSource.Imdb]));
		_defaults.FindAsync(Any()).Returns(Some.Of(stored));

		await Assert.That(await Testee.GetDefaultAsync()).IsEqualTo(stored);
	}
}
