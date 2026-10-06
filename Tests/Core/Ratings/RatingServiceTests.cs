using Contracts.Providers;
using Contracts.Ratings;
using Core.Ratings;
using Domain.Media;
using Domain.Providers;

namespace Core.Tests.Ratings;

public class RatingServiceTests
{
	private readonly Mock<IRatingDefaultRepository> _defaults = IRatingDefaultRepository.Mock();
	private readonly Mock<IServiceReadiness> _readiness = IServiceReadiness.Mock();
	private readonly HashSet<RemoteService> _connected = [];

	public RatingServiceTests()
	{
		_readiness.ConnectedAsync(Any(), Any()).Returns((asked, _) => asked.Where(_connected.Contains).ToHashSet());
	}

	private RatingService Testee => new(_defaults.Object, _readiness.Object);

	[Test]
	public async Task GetAvailableAsync_WithOnlyTmdbConnected_OffersOnlyTmdb()
	{
		_connected.Add(RemoteService.Tmdb);

		await Assert.That(await Testee.GetAvailableAsync()).IsEquivalentTo([RatingSource.Tmdb]);
	}

	[Test]
	public async Task GetAvailableAsync_WithPlexConnected_OffersEverySourcePlexSupplies()
	{
		_connected.Add(RemoteService.Plex);

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
	public async Task GetAvailableAsync_NeverAsksAfterAServiceThatSuppliesNoRating()
	{
		await Testee.GetAvailableAsync();

		_readiness.ConnectedAsync(Is<IEnumerable<RemoteService>>(asked => asked!.Contains(RemoteService.Mdblist)), Any()).WasNeverCalled();
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
