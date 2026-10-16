using Contracts.Datasets;
using Contracts.Providers;
using Contracts.Ratings;
using Core.Ratings;
using Domain.Media;
using Domain.Providers;
using TestInfrastructure;

namespace Core.Tests.Ratings;

public class RatingServiceTests
{
	private readonly Mock<IRatingDefaultRepository> _defaults = IRatingDefaultRepository.Mock();
	private readonly Mock<IServiceReadiness> _readiness = IServiceReadiness.Mock();
	private readonly Mock<IDatasetLoads> _datasets = IDatasetLoads.Mock();
	private readonly HashSet<RemoteService> _connected = [];

	public RatingServiceTests()
	{
		_readiness.ConnectedAsync(Any(), Any()).Returns((asked, _) => asked.Where(_connected.Contains).ToHashSet());
	}

	private RatingService Testee => new(_defaults.Object, _readiness.Object, _datasets.Object);

	[Test]
	public async Task GetImdbFileDateAsync_AfterALoad_IsWhenTheFileWasLoaded()
	{
		var loadedAt = new DateTimeOffset(2026, 10, 6, 4, 30, 0, TimeSpan.Zero);
		_datasets.FindAsync(DatasetNames.ImdbRatings, Any()).Returns(Some.Of(new DatasetLoad(DatasetNames.ImdbRatings, loadedAt, new None(), 1_717_695)));

		await Assert.That(await Testee.GetImdbFileDateAsync()).IsSome().And.IsEqualTo(loadedAt);
	}

	[Test]
	public async Task GetImdbFileDateAsync_BeforeTheFirstLoad_IsNone()
	{
		_datasets.FindAsync(Any(), Any()).Returns(new None());

		await Assert.That(await Testee.GetImdbFileDateAsync()).IsNone();
	}

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
	public async Task GetAvailableAsync_WithOnlyMdblistConnected_OffersEverySourceMdblistRates()
	{
		_connected.Add(RemoteService.Mdblist);

		await Assert.That(await Testee.GetAvailableAsync()).IsEquivalentTo(Enum.GetValues<RatingSource>().Where(source => source is not RatingSource.AniList));
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
