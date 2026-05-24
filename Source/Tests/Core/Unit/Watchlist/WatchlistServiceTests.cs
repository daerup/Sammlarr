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
		var mediaId = MediaId.From(Guid.NewGuid());
		var addedAt = _faker.Date.PastOffset();
		var entry = new WatchlistedMedia(mediaId, addedAt, new WatchlistItemProvenance(new ManualOrigin()));
		var detail = CreateMovieDetailResult(mediaId);

		var scenario = Scenario.Build(entries: [entry], metadata: detail);

		var result = await scenario.Testee.GetWatchlistItemsAsync().ToListAsync();

		await Assert.That(result).Count().IsEqualTo(1);
		await Assert.That(result[0].Detail.Id).IsEqualTo(mediaId);
		await Assert.That(result[0].AddedAt).IsEqualTo(addedAt);
	}

	[Test]
	public async Task GetWatchlistIdsAsync_ReturnsRepositoryIds()
	{
		var first = MediaId.From(Guid.NewGuid());
		var second = MediaId.From(Guid.NewGuid());
		var scenario = Scenario.Build(
			entries:
			[
				new WatchlistedMedia(first, _faker.Date.PastOffset(), new WatchlistItemProvenance(new ManualOrigin())),
				new WatchlistedMedia(second, _faker.Date.PastOffset(), new WatchlistItemProvenance(new ManualOrigin())),
			]
		);

		var result = await scenario.Testee.GetWatchlistIdsAsync();

		await Assert.That(result).Contains(first);
		await Assert.That(result).Contains(second);
	}

	[Test]
	public async Task AddToWatchlistAsync_BrandNewId_EnqueuesOutboxAdd()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var scenario = Scenario.Build();

		await scenario.Testee.AddToWatchlistAsync([mediaId], new WatchlistItemProvenance(new ManualOrigin()));

		scenario.Repository.AddAsync(Any<IEnumerable<MediaId>>(), Any<WatchlistItemProvenance>(), Any()).WasCalled(Times.Once);
		scenario.Outbox.EnqueueAsync(WatchlistOperationKind.Add, Any<IEnumerable<MediaId>>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task AddToWatchlistAsync_EmptyCollection_DoesNothing()
	{
		var scenario = Scenario.Build();

		await scenario.Testee.AddToWatchlistAsync([], new WatchlistItemProvenance(new ManualOrigin()));

		scenario.Repository.AddAsync(Any<IEnumerable<MediaId>>(), Any<WatchlistItemProvenance>(), Any()).WasNeverCalled();
		scenario.Outbox.EnqueueAsync(Any<WatchlistOperationKind>(), Any<IEnumerable<MediaId>>(), Any()).WasNeverCalled();
		scenario.Outbox.DropAsync(Any<WatchlistOperationKind>(), Any<IEnumerable<MediaId>>(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task AddToWatchlistAsync_WhenPendingRemoveExists_DropsRemoveInstead()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var scenario = Scenario.Build(outbox: [new WatchlistOperation(mediaId, WatchlistOperationKind.Remove)]);

		await scenario.Testee.AddToWatchlistAsync([mediaId], new WatchlistItemProvenance(new ManualOrigin()));

		scenario.Outbox.DropAsync(Any<WatchlistOperationKind>(), Any<IEnumerable<MediaId>>(), Any()).WasCalled(Times.Once);
		scenario.Repository.AddAsync(Any<IEnumerable<MediaId>>(), Any<WatchlistItemProvenance>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task AddToWatchlistAsync_AlreadySyncedId_DoesNotEnqueueAdd()
	{
		var syncedId = MediaId.From(Guid.NewGuid());
		var scenario = Scenario.Build(existingIds: [syncedId]);

		await scenario.Testee.AddToWatchlistAsync([syncedId], new WatchlistItemProvenance(new ManualOrigin()));

		scenario.Repository.AddAsync(Any<IEnumerable<MediaId>>(), Any<WatchlistItemProvenance>(), Any()).WasCalled(Times.Once);
		scenario.Outbox.EnqueueAsync(WatchlistOperationKind.Add, Arg.Is<IEnumerable<MediaId>>(ids => ids.Any()), Any()).WasNeverCalled();
	}

	[Test]
	public async Task AddToWatchlistAsync_MixedBatch_PartitionsUncancelAndPush()
	{
		var pendingRemoveId = MediaId.From(Guid.NewGuid());
		var brandNewId = MediaId.From(Guid.NewGuid());
		var syncedId = MediaId.From(Guid.NewGuid());
		var scenario = Scenario.Build(outbox: [new WatchlistOperation(pendingRemoveId, WatchlistOperationKind.Remove)], existingIds: [syncedId]);

		await scenario.Testee.AddToWatchlistAsync([pendingRemoveId, brandNewId, syncedId], new WatchlistItemProvenance(new ManualOrigin()));

		scenario
			.Outbox.DropAsync(WatchlistOperationKind.Remove, Arg.Is<IEnumerable<MediaId>>(ids => ids.SequenceEqual(new[] { pendingRemoveId })), Any())
			.WasCalled(Times.Once);
		scenario
			.Outbox.EnqueueAsync(WatchlistOperationKind.Add, Arg.Is<IEnumerable<MediaId>>(ids => ids.SequenceEqual(new[] { brandNewId })), Any())
			.WasCalled(Times.Once);
		scenario.Repository.AddAsync(Any<IEnumerable<MediaId>>(), Any<WatchlistItemProvenance>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task RemoveFromWatchlistAsync_WhenSynced_EnqueuesOutboxRemove()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var scenario = Scenario.Build();

		await scenario.Testee.RemoveFromWatchlistAsync([mediaId]);

		scenario.Outbox.EnqueueAsync(WatchlistOperationKind.Remove, Any<IEnumerable<MediaId>>(), Any()).WasCalled(Times.Once);
		scenario.Repository.RemoveAsync(Any<IEnumerable<MediaId>>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task RemoveFromWatchlistAsync_WhenUnpushedAdd_CancelsAddOnly()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var scenario = Scenario.Build(outbox: [new WatchlistOperation(mediaId, WatchlistOperationKind.Add)]);

		await scenario.Testee.RemoveFromWatchlistAsync([mediaId]);

		scenario.Outbox.DropAsync(Any<WatchlistOperationKind>(), Any<IEnumerable<MediaId>>(), Any()).WasCalled(Times.Once);
		scenario.Outbox.EnqueueAsync(WatchlistOperationKind.Remove, Any<IEnumerable<MediaId>>(), Any()).WasNeverCalled();
		scenario.Repository.RemoveAsync(Any<IEnumerable<MediaId>>(), Any()).WasCalled(Times.Once);
	}

	private MovieDetailResult CreateMovieDetailResult(MediaId mediaId) =>
		new()
		{
			Id = mediaId,
			Externals = new List<IExternalMediaId> { PlexId.From(_faker.Random.AlphaNumeric(10), MediaType.Movie) },
			Title = _faker.Lorem.Sentence(2),
			Tagline = _faker.Company.CatchPhrase(),
			Summary = _faker.Lorem.Paragraph(),
			PosterUrl = _faker.Internet.Url(),
			BackdropUrl = _faker.Internet.Url(),
			Runtime = TimeSpan.FromMinutes(_faker.Random.Int(60, 180)),
			ReleaseDate = _faker.Date.PastDateOnly(),
			Genres = [],
		};

	private sealed record Scenario(
		Mock<IWatchlistRepository> Repository,
		Mock<IWatchlistOutbox> Outbox,
		Mock<IMediaMetadataProvider> Metadata,
		Mock<IMediaRepository> MediaRepo,
		Mock<IProviderConfigRepository> Config,
		WatchlistService Testee
	)
	{
		public static Scenario Build(
			IReadOnlyList<WatchlistedMedia>? entries = null,
			IReadOnlyList<WatchlistOperation>? outbox = null,
			IReadOnlyList<MediaId>? existingIds = null,
			MovieDetailResult? metadata = null,
			IReadOnlyDictionary<MediaId, IExternalMediaId>? watchlistProviderExternals = null
		)
		{
			var repository = IWatchlistRepository.Mock();
			repository.GetEntriesAsync(Any()).Returns(entries ?? []);
			repository.GetIdsAsync(Any()).Returns(existingIds ?? []);

			var outboxMock = IWatchlistOutbox.Mock();
			outboxMock.GetAsync(Any()).Returns(outbox ?? []);

			var metadataProvider = IMediaMetadataProvider.Mock();
			if (metadata is not null)
			{
				metadataProvider.GetAsync(Any<MediaId>(), Any()).Returns(metadata);
			}

			var mediaRepo = IMediaRepository.Mock();
			mediaRepo
				.GetExternalsAsync(Any<IEnumerable<MediaId>>(), Any<MediaProvider>(), Any())
				.Returns(watchlistProviderExternals ?? new PermissiveExternalsDictionary());

			var config = IProviderConfigRepository.Mock();
			config
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

			var testee = new WatchlistService(repository.Object, outboxMock.Object, metadataProvider.Object, mediaRepo.Object, config.Object);
			return new Scenario(repository, outboxMock, metadataProvider, mediaRepo, config, testee);
		}
	}

	private sealed class PermissiveExternalsDictionary : IReadOnlyDictionary<MediaId, IExternalMediaId>
	{
		public bool ContainsKey(MediaId key) => true;

		public bool TryGetValue(MediaId key, out IExternalMediaId value)
		{
			value = PlexId.From(key.ToString(), MediaType.Movie);
			return true;
		}

		public IExternalMediaId this[MediaId key] => PlexId.From(key.ToString(), MediaType.Movie);
		public IEnumerable<MediaId> Keys => [];
		public IEnumerable<IExternalMediaId> Values => [];
		public int Count => int.MaxValue;

		public IEnumerator<KeyValuePair<MediaId, IExternalMediaId>> GetEnumerator() =>
			Enumerable.Empty<KeyValuePair<MediaId, IExternalMediaId>>().GetEnumerator();

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
