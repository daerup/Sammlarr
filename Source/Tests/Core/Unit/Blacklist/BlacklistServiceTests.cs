using Bogus;
using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Blacklist;
using Contracts.Media.Search;
using Contracts.Media.Trending;
using Contracts.Settings;
using Core.Blacklist;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Tests.Unit.Blacklist;

public class BlacklistServiceTests
{
	private readonly Faker _faker = new();

	[Test]
	public async Task IsBlacklistedAsync_WhenMediaIdIsBlacklisted_ReturnsTrue()
	{
		var mediaId = CreateMediaId();
		var repository = IBlacklistRepository.Mock();
		repository.IsBlacklistedAsync(Any(), Any()).Returns(true);
		var testee = CreateTestee(repository: repository);

		var result = await testee.IsBlacklistedAsync(mediaId);

		await Assert.That(result).IsTrue();
	}

	[Test]
	public async Task IsBlacklistedAsync_WhenMediaIdIsNotBlacklisted_ReturnsFalse()
	{
		var mediaId = CreateMediaId();
		var repository = IBlacklistRepository.Mock();
		repository.IsBlacklistedAsync(Any(), Any()).Returns(false);
		var testee = CreateTestee(repository: repository);

		var result = await testee.IsBlacklistedAsync(mediaId);

		await Assert.That(result).IsFalse();
	}

	[Test]
	public async Task AddToBacklistAsync_WhenCalled_DelegatesToRepository()
	{
		var mediaId = CreateMediaId();
		var repository = IBlacklistRepository.Mock();
		var testee = CreateTestee(repository: repository);

		await testee.AddToBacklistAsync(mediaId);

		repository.AddAsync(Any(), Any()).WasCalled();
	}

	[Test]
	public async Task RemoveFromBacklistAsync_WhenCalled_DelegatesToRepository()
	{
		var mediaId = CreateMediaId();
		var repository = IBlacklistRepository.Mock();
		var testee = CreateTestee(repository: repository);

		await testee.RemoveFromBacklistAsync(mediaId);

		repository.RemoveAsync(Any(), Any()).WasCalled();
	}

	[Test]
	public async Task GetBlacklistItems_WhenEntriesExist_ReturnsItemsMappedFromSearchService()
	{
		var addedDate = _faker.Date.PastOffset();
		var mediaId = CreateMediaId();
		var entry = new BlacklistEntry(mediaId, addedDate);
		var detailResult = CreateMovieDetailResult(mediaId);

		var repository = IBlacklistRepository.Mock();
		repository.GetBlacklistedEntriesAsync(Any(), Any()).Returns([entry]);

		var searchService = IMediaSearchService.Mock();
		searchService.LookUpAsync(Any(), Any()).Returns(detailResult);

		var testee = CreateTestee(repository: repository, searchService: searchService);

		var result = await testee.GetBlacklistItems(MediaProvider.Plex);

		await Assert.That(result).Count().IsEqualTo(1);
		var item = result[0];
		await Assert.That(item.SourceId).IsEqualTo(mediaId);
		await Assert.That(item.Title).IsEqualTo(detailResult.Title);
		await Assert.That(item.BlacklistDate).IsEqualTo(addedDate);
	}

	[Test]
	public async Task GetBlacklistItems_WhenNoEntriesExist_ReturnsEmptyList()
	{
		var repository = IBlacklistRepository.Mock();
		repository.GetBlacklistedEntriesAsync(Any(), Any()).Returns(new List<BlacklistEntry>());

		var testee = CreateTestee(repository: repository);

		var result = await testee.GetBlacklistItems(MediaProvider.Plex);

		await Assert.That(result).IsEmpty();
	}

	private BlacklistService CreateTestee(Mock<IBlacklistRepository>? repository = null, Mock<IMediaSearchService>? searchService = null)
	{
		var repo = repository ?? IBlacklistRepository.Mock();
		var providerConfigRepository = IProviderConfigRepository.Mock();
		providerConfigRepository
			.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					WatchlistProvider = WatchlistProvider.Plex,
					TrendingProvider = MediaRecommendationProvider.Tmdb,
					SearchProvider = MediaSearchProvider.Plex,
				}
			);

		var search = (searchService ?? IMediaSearchService.Mock()).Object;
		var services = new ServiceCollection();
		services.AddKeyedSingleton<IMediaSearchService>(MediaSearchProvider.Plex, (_, _) => search);
		var serviceProvider = services.BuildServiceProvider();

		return new BlacklistService(repo.Object, providerConfigRepository.Object, serviceProvider);
	}

	private IMediaId CreateMediaId() => PlexId.From(_faker.Random.AlphaNumeric(10), MediaType.Movie);

	private MovieDetailResult CreateMovieDetailResult(IMediaId sourceId) =>
		new()
		{
			SourceId = sourceId,
			Title = _faker.Lorem.Sentence(2),
			AdditionalIds = [],
			Year = _faker.Date.Past().Year,
			PosterUrl = _faker.Internet.Url(),
			BackdropUrl = _faker.Internet.Url(),
			Runtime = TimeSpan.FromMinutes(_faker.Random.Int(60, 180)),
			ReleaseDate = _faker.Date.PastDateOnly(),
			Genres = [],
		};
}
