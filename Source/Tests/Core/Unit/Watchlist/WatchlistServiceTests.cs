using Bogus;
using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Media.Trending;
using Contracts.Settings;
using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Core.Watchlist;
using Test.Shared;

namespace Core.Tests.Unit.Watchlist;

public class WatchlistServiceTests
{
	private readonly Faker _faker = new();

	[Test]
	public async Task GetWatchlistItemsAsync_ReadsLocalRepo_EnrichesWithMetadata()
	{
		var mediaId = CreateMediaId();
		var addedAt = _faker.Date.PastOffset();
		var entry = new WatchlistEntry(mediaId, addedAt, new WatchlistItemProvenance(new ManualOrigin()));
		var detail = CreateMovieDetailResult(mediaId);

		var repository = IWatchlistRepository.Mock();
		repository.GetEntriesAsync(Any(), Any()).Returns([entry]);

		var metadataProvider = IMediaMetadataProvider.Mock();
		metadataProvider.GetAsync(Any<IMediaId>(), Any()).Returns(detail);

		var testee = CreateTestee(repository: repository, metadataProvider: metadataProvider);

		var result = await testee.GetWatchlistItemsAsync().ToListAsync();

		await Assert.That(result).Count().IsEqualTo(1);
		await Assert.That(result[0].Detail.SourceId).IsEqualTo(mediaId);
		await Assert.That(result[0].AddedAt).IsEqualTo(addedAt);
	}

	[Test]
	public async Task GetWatchlistIdsAsync_ReturnsRepositoryIds()
	{
		IMediaId first = PlexId.From("plex-1", MediaType.Movie);
		IMediaId second = PlexId.From("plex-2", MediaType.Movie);
		IReadOnlySet<IMediaId> ids = new HashSet<IMediaId> { first, second };
		var repository = IWatchlistRepository.Mock();
		repository
			.GetEntriesAsync(Any(), Any())
			.Returns(ids.Select(id => new WatchlistEntry(id, _faker.Date.PastOffset(), new WatchlistItemProvenance(new ManualOrigin()))).ToList());

		var testee = CreateTestee(repository: repository);

		var result = await testee.GetWatchlistIdsAsync();

		await Assert.That(result.Contains(first)).IsTrue();
		await Assert.That(result.Contains(PlexId.From("missing", MediaType.Movie))).IsFalse();
	}

	[Test]
	public async Task AddToWatchlistAsync_BrandNewId_EnqueuesOutboxAdd_DoesNotPush()
	{
		var mediaId = CreateMediaId();
		var provider = IWatchlistProvider.Mock();
		var repository = IWatchlistRepository.Mock();
		repository.GetIdsAsync(Any(), Any()).Returns([]);
		var outbox = IWatchlistOutbox.Mock();
		outbox.GetAsync(Any(), Any()).Returns([]);

		var testee = CreateTestee(provider: provider, repository: repository, outbox: outbox);

		await testee.AddToWatchlistAsync([mediaId], new ManualOrigin());

		repository.AddAsync(Any<IEnumerable<IMediaId>>(), Any(), Any()).WasCalled(Times.Once);
		outbox.EnqueueAsync(WatchlistOperationKind.Add, Any<IEnumerable<IMediaId>>(), Any()).WasCalled(Times.Once);
		provider.AddToWatchlistAsync(Any<IMediaId>(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task AddToWatchlistAsync_WhenPendingRemoveExists_DropsRemove_NoOutboxAdd()
	{
		var mediaId = CreateMediaId();
		var repository = IWatchlistRepository.Mock();
		repository.GetIdsAsync(Any(), Any()).Returns([]);
		var outbox = IWatchlistOutbox.Mock();
		outbox.GetAsync(Any(), Any()).Returns([new WatchlistOperation(mediaId, WatchlistOperationKind.Remove)]);

		var testee = CreateTestee(repository: repository, outbox: outbox);

		await testee.AddToWatchlistAsync([mediaId], new ManualOrigin());

		outbox.DropAsync(WatchlistOperationKind.Remove, Any<IEnumerable<IMediaId>>(), Any()).WasCalled(Times.Once);
		outbox.EnqueueAsync(WatchlistOperationKind.Add, Any<IEnumerable<IMediaId>>(), Any()).WasCalled(Times.Once);
		repository.AddAsync(Any<IEnumerable<IMediaId>>(), Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task RemoveFromWatchlistAsync_WhenSynced_EnqueuesOutboxRemove()
	{
		var mediaId = CreateMediaId();
		var provider = IWatchlistProvider.Mock();
		var repository = IWatchlistRepository.Mock();
		var outbox = IWatchlistOutbox.Mock();
		outbox.GetAsync(Any(), Any()).Returns([]);

		var testee = CreateTestee(provider: provider, repository: repository, outbox: outbox);

		await testee.RemoveFromWatchlistAsync(mediaId);

		outbox.EnqueueAsync(WatchlistOperationKind.Remove, Any<IEnumerable<IMediaId>>(), Any()).WasCalled(Times.Once);
		repository.RemoveAsync(Any<IEnumerable<IMediaId>>(), Any()).WasCalled(Times.Once);
		provider.RemoveFromWatchlistAsync(Any<IMediaId>(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task RemoveFromWatchlistAsync_WhenUnpushedAdd_DropsOutboxAdd_NoEnqueueRemove()
	{
		var mediaId = CreateMediaId();
		var repository = IWatchlistRepository.Mock();
		var outbox = IWatchlistOutbox.Mock();
		outbox.GetAsync(Any(), Any()).Returns([new WatchlistOperation(mediaId, WatchlistOperationKind.Add)]);

		var testee = CreateTestee(repository: repository, outbox: outbox);

		await testee.RemoveFromWatchlistAsync(mediaId);

		outbox.DropAsync(WatchlistOperationKind.Add, Any<IEnumerable<IMediaId>>(), Any()).WasCalled(Times.Once);
		outbox.EnqueueAsync(WatchlistOperationKind.Remove, Any<IEnumerable<IMediaId>>(), Any()).WasNeverCalled();
		repository.RemoveAsync(Any<IEnumerable<IMediaId>>(), Any()).WasCalled(Times.Once);
	}

	private WatchlistService CreateTestee(
		Mock<IWatchlistProvider>? provider = null,
		Mock<IWatchlistRepository>? repository = null,
		Mock<IWatchlistOutbox>? outbox = null,
		Mock<IMediaMetadataProvider>? metadataProvider = null
	)
	{
		var configRepository = IProviderConfigRepository.Mock();
		configRepository
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

		var repo = repository ?? IWatchlistRepository.Mock();
		var outboxMock = outbox ?? IWatchlistOutbox.Mock();
		var metadata = metadataProvider ?? IMediaMetadataProvider.Mock();

		return new WatchlistService(configRepository.Object, repo.Object, outboxMock.Object, metadata.Object);
	}

	private PlexId CreateMediaId() => PlexId.From(_faker.Random.AlphaNumeric(10), MediaType.Movie);

	private MovieDetailResult CreateMovieDetailResult(PlexId sourceId) =>
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

	private static async IAsyncEnumerable<T> AsAsyncEnumerable<T>(params T[] items)
	{
		foreach (var item in items)
		{
			yield return item;
			await Task.CompletedTask;
		}
	}
}
