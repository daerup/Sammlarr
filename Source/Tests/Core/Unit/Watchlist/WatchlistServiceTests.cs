using Bogus;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
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
		var entry = new WatchlistedMedia(mediaId, addedAt, new WatchlistItemProvenance(new ManualOrigin()));
		var detail = CreateMovieDetailResult(mediaId);

		var scenario = Scenario.Build(entries: [entry], metadata: detail);

		var result = await scenario.Testee.GetWatchlistItemsAsync().ToListAsync();

		await Assert.That(result).Count().IsEqualTo(1);
		await Assert.That(result[0].Detail.SourceId).IsEqualTo(mediaId);
		await Assert.That(result[0].AddedAt).IsEqualTo(addedAt);
	}

	[Test]
	public async Task GetWatchlistIdsAsync_ReturnsRepositoryIds()
	{
		IMediaId first = CreateMediaId();
		IMediaId second = CreateMediaId();
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
		var mediaId = CreateMediaId();
		var scenario = Scenario.Build();

		await scenario.Testee.AddToWatchlistAsync([mediaId], new ManualOrigin());

		scenario.Repository.AddAsync(Any<IEnumerable<IMediaId>>(), Any(), Any()).WasCalled(Times.Once);
		scenario.Outbox.EnqueueAsync(WatchlistOperationKind.Add, Any<IEnumerable<IMediaId>>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task AddToWatchlistAsync_EmptyCollection_DoesNothing()
	{
		var scenario = Scenario.Build();

		await scenario.Testee.AddToWatchlistAsync([], new ManualOrigin());

		scenario.Repository.AddAsync(Any<IEnumerable<IMediaId>>(), Any(), Any()).WasNeverCalled();
		scenario.Outbox.EnqueueAsync(Any<WatchlistOperationKind>(), Any<IEnumerable<IMediaId>>(), Any()).WasNeverCalled();
		scenario.Outbox.DropAsync(Any<WatchlistOperationKind>(), Any<IEnumerable<IMediaId>>(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task AddToWatchlistAsync_WhenPendingRemoveExists_DropsRemoveInstead()
	{
		var mediaId = CreateMediaId();
		var scenario = Scenario.Build(outbox: [new WatchlistOperation(mediaId, WatchlistOperationKind.Remove)]);

		await scenario.Testee.AddToWatchlistAsync([mediaId], new ManualOrigin());

		scenario.Outbox.DropAsync(WatchlistOperationKind.Remove, Any<IEnumerable<IMediaId>>(), Any()).WasCalled(Times.Once);
		scenario.Repository.AddAsync(Any<IEnumerable<IMediaId>>(), Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task AddToWatchlistAsync_AlreadySyncedId_DoesNotEnqueueAdd()
	{
		IMediaId syncedId = CreateMediaId();
		var scenario = Scenario.Build(existingIds: [syncedId]);

		await scenario.Testee.AddToWatchlistAsync([syncedId], new ManualOrigin());

		scenario.Repository.AddAsync(Any<IEnumerable<IMediaId>>(), Any(), Any()).WasCalled(Times.Once);
		scenario.Outbox.EnqueueAsync(WatchlistOperationKind.Add, Arg.Is<IEnumerable<IMediaId>>(ids => ids.Any()), Any()).WasNeverCalled();
	}

	[Test]
	public async Task AddToWatchlistAsync_MixedBatch_PartitionsUncancelAndPush()
	{
		IMediaId pendingRemoveId = CreateMediaId();
		IMediaId brandNewId = CreateMediaId();
		IMediaId syncedId = CreateMediaId();
		var scenario = Scenario.Build(outbox: [new WatchlistOperation(pendingRemoveId, WatchlistOperationKind.Remove)], existingIds: [syncedId]);

		await scenario.Testee.AddToWatchlistAsync([pendingRemoveId, brandNewId, syncedId], new ManualOrigin());

		scenario
			.Outbox.DropAsync(WatchlistOperationKind.Remove, Arg.Is<IEnumerable<IMediaId>>(ids => ids.SequenceEqual(new[] { pendingRemoveId })), Any())
			.WasCalled(Times.Once);
		scenario
			.Outbox.EnqueueAsync(WatchlistOperationKind.Add, Arg.Is<IEnumerable<IMediaId>>(ids => ids.SequenceEqual(new[] { brandNewId })), Any())
			.WasCalled(Times.Once);
		scenario.Repository.AddAsync(Any<IEnumerable<IMediaId>>(), Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task RemoveFromWatchlistAsync_WhenSynced_EnqueuesOutboxRemove()
	{
		var mediaId = CreateMediaId();
		var scenario = Scenario.Build();

		await scenario.Testee.RemoveFromWatchlistAsync(mediaId);

		scenario.Outbox.EnqueueAsync(WatchlistOperationKind.Remove, Any<IEnumerable<IMediaId>>(), Any()).WasCalled(Times.Once);
		scenario.Repository.RemoveAsync(Any<IEnumerable<IMediaId>>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task RemoveFromWatchlistAsync_WhenUnpushedAdd_CancelsAddOnly()
	{
		var mediaId = CreateMediaId();
		var scenario = Scenario.Build(outbox: [new WatchlistOperation(mediaId, WatchlistOperationKind.Add)]);

		await scenario.Testee.RemoveFromWatchlistAsync(mediaId);

		scenario.Outbox.DropAsync(WatchlistOperationKind.Add, Any<IEnumerable<IMediaId>>(), Any()).WasCalled(Times.Once);
		scenario.Outbox.EnqueueAsync(WatchlistOperationKind.Remove, Any<IEnumerable<IMediaId>>(), Any()).WasNeverCalled();
		scenario.Repository.RemoveAsync(Any<IEnumerable<IMediaId>>(), Any()).WasCalled(Times.Once);
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

	private sealed record Scenario(
		Mock<IWatchlistRepository> Repository,
		Mock<IWatchlistOutbox> Outbox,
		Mock<IMediaMetadataProvider> Metadata,
		WatchlistService Testee
	)
	{
		public static Scenario Build(
			IReadOnlyList<WatchlistedMedia>? entries = null,
			IReadOnlyList<WatchlistOperation>? outbox = null,
			IReadOnlyList<IMediaId>? existingIds = null,
			MovieDetailResult? metadata = null
		)
		{
			var repository = IWatchlistRepository.Mock();
			repository.GetEntriesAsync(Any(), Any()).Returns(entries ?? []);
			repository.GetIdsAsync(Any(), Any()).Returns(existingIds ?? []);

			var outboxMock = IWatchlistOutbox.Mock();
			outboxMock.GetAsync(Any(), Any()).Returns(outbox ?? []);

			var metadataProvider = IMediaMetadataProvider.Mock();
			if (metadata is not null)
			{
				metadataProvider.GetAsync(Any<IMediaId>(), Any()).Returns(metadata);
			}

			var testee = new WatchlistService(ProviderConfigStubs.PlexWatchlist().Object, repository, outboxMock, metadataProvider);
			return new Scenario(repository, outboxMock, metadataProvider, testee);
		}
	}
}
