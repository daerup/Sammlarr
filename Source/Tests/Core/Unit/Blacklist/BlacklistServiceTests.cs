using Bogus;
using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Blacklist;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Media.Trending;
using Contracts.Settings;
using Core.Blacklist;

namespace Core.Tests.Unit.Blacklist;

public class BlacklistServiceTests
{
	private readonly Faker _faker = new();

	[Test]
	public async Task AddToBacklistAsync_WhenCalled_DelegatesToRepository()
	{
		var mediaId = CreateMediaId();
		var repository = IBlacklistRepository.Mock();
		var testee = CreateTestee(repository);

		await testee.AddToBacklistAsync(mediaId);

		repository.AddAsync(Any(), Any()).WasCalled();
	}

	[Test]
	public async Task RemoveFromBacklistAsync_WhenCalled_DelegatesToRepository()
	{
		var mediaId = CreateMediaId();
		var repository = IBlacklistRepository.Mock();
		var testee = CreateTestee(repository);

		await testee.RemoveFromBacklistAsync(mediaId);

		repository.RemoveAsync(Any(), Any()).WasCalled();
	}

	[Test]
	public async Task GetBlacklistItems_WhenEntriesExist_ReturnsItemsMappedFromMetadataProvider()
	{
		var addedAt = _faker.Date.PastOffset();
		var mediaId = CreateMediaId();
		var entry = new BlacklistedMedia(mediaId, addedAt);
		var detailResult = CreateMovieDetailResult(mediaId);

		var repository = IBlacklistRepository.Mock();
		repository.GetBlacklistedEntriesAsync(Any(), Any()).Returns([entry]);

		var metadataProvider = IMediaMetadataProvider.Mock();
		metadataProvider.GetAsync(Any<IMediaId>(), Any()).Returns(detailResult);

		var testee = CreateTestee(repository, metadataProvider);

		var result = await testee.GetBlacklistItems().ToListAsync();

		await Assert.That(result).Count().IsEqualTo(1);
		var item = result[0];
		await Assert.That(item.Detail.SourceId).IsEqualTo(mediaId);
		await Assert.That(item.Detail.Title).IsEqualTo(detailResult.Title);
		await Assert.That(item.AddedAt).IsEqualTo(addedAt);
	}

	[Test]
	public async Task GetBlacklistItems_WhenNoEntriesExist_ReturnsEmptyList()
	{
		var repository = IBlacklistRepository.Mock();
		repository.GetBlacklistedEntriesAsync(Any(), Any()).Returns(new List<BlacklistedMedia>());

		var testee = CreateTestee(repository);

		var result = await testee.GetBlacklistItems().ToListAsync();

		await Assert.That(result).IsEmpty();
	}

	private BlacklistService CreateTestee(Mock<IBlacklistRepository>? repository = null, Mock<IMediaMetadataProvider>? metadataProvider = null)
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
					RatingProvider = RatingProvider.Tmdb,
				}
			);

		var provider = metadataProvider ?? IMediaMetadataProvider.Mock();
		return new BlacklistService(repo.Object, providerConfigRepository.Object, provider.Object);
	}

	private IMediaId CreateMediaId() => PlexId.From(_faker.Random.AlphaNumeric(10), MediaType.Movie);

	private MovieDetailResult CreateMovieDetailResult(IMediaId sourceId) =>
		new()
		{
			SourceId = sourceId,
			Title = _faker.Lorem.Sentence(2),
			Tagline = _faker.Company.CatchPhrase(),
			Summary = _faker.Lorem.Paragraph(),
			AdditionalIds = [],
			PosterUrl = _faker.Internet.Url(),
			BackdropUrl = _faker.Internet.Url(),
			Runtime = TimeSpan.FromMinutes(_faker.Random.Int(60, 180)),
			ReleaseDate = _faker.Date.PastDateOnly(),
			Genres = [],
		};
}
