using Bogus;
using Contracts;
using Contracts.Media;
using Contracts.Media.Search;
using Contracts.Messaging;
using Contracts.Settings;
using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Core.Watchlist;
using Domain.Media;
using Domain.Notifications.Events;
using Domain.Watchlist;
using Domain.Watchlist.Outbox;
using TestInfrastructure;

namespace Core.Tests.Watchlist;

public class WatchlistServiceTests
{
	private readonly Faker _faker = new();

	[Test]
	public async Task GetWatchlistItemsAsync_IncludeRemoved_MergesRemovedItemsAtTheirOriginalAddedAt()
	{
		var activeId = MediaId.From(Guid.NewGuid());
		var removedId = MediaId.From(Guid.NewGuid());
		var activeAddedAt = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);
		var removedAddedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

		var repository = IWatchlistRepository.Mock();
		repository.GetEntriesAsync(Any()).Returns([new WatchlistEntry(activeId, activeAddedAt, new ManualOrigin(ManualSource.Search))]);

		var outbox = IWatchlistOutbox.Mock();
		outbox
			.GetAsync(Any())
			.Returns([new WatchlistOutboxEntry(removedId, WatchlistOperationAction.Remove, WatchlistOutboxState.Pending, 0) { AddedAt = removedAddedAt }]);

		IdentifiedMedia active = CreateMovie(activeId);
		IdentifiedMedia removed = CreateMovie(removedId);
		var media = IMediaService.Mock();
		media.GetDetailsAsync(Any(), Any()).Returns(Async<Result<IdentifiedMedia>>(Success.Of(active), Success.Of(removed)));

		var testee = CreateTestee(repository, outbox, media.Object);

		var result = await testee.GetWatchlistItemsAsync(WatchlistQueryOptions.WithRemoved).ToListAsync();

		await Assert.That(result.Select(item => item.Media.Id)).Contains(removedId);
		var removedItem = result.First(item => item.Media.Id == removedId);
		await Assert.That(removedItem.AddedAt).IsEqualTo(removedAddedAt);
		await Assert.That(result[0].Media.Id).IsEqualTo(activeId);
	}

	[Test]
	public async Task GetWatchlistItemsAsync_WithoutIncludeRemoved_ExcludesRemovedItemsAndSkipsOutbox()
	{
		var activeId = MediaId.From(Guid.NewGuid());

		var repository = IWatchlistRepository.Mock();
		repository.GetEntriesAsync(Any()).Returns([new WatchlistEntry(activeId, _faker.Date.PastOffset(), new ManualOrigin(ManualSource.Search))]);

		IdentifiedMedia active = CreateMovie(activeId);
		var media = IMediaService.Mock();
		media.GetDetailsAsync(Any(), Any()).Returns(Async<Result<IdentifiedMedia>>(Success.Of(active)));

		var outbox = IWatchlistOutbox.Mock();

		var testee = CreateTestee(repository, outbox, media.Object);

		var result = await testee.GetWatchlistItemsAsync(WatchlistQueryOptions.Default).ToListAsync();

		await Assert.That(result.Select(item => item.Media.Id)).Contains(activeId);
		outbox.GetAsync(Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task AddToWatchlistAsync_WhenRemovalPending_DropsTheRemoveAndRestoresLocally()
	{
		var mediaId = MediaId.From(Guid.NewGuid());

		var repository = IWatchlistRepository.Mock();
		repository.GetIdsAsync(Any()).Returns([]);
		repository.AddAsync(Any<IEnumerable<MediaId>>(), Any<WatchlistItemProvenance>(), Any()).Returns([]);

		var outbox = IWatchlistOutbox.Mock();
		outbox.GetAsync(Any()).Returns([new WatchlistOutboxEntry(mediaId, WatchlistOperationAction.Remove, WatchlistOutboxState.Pending, 0)]);

		var testee = CreateTestee(repository, outbox, IMediaService.Mock().Object);

		await testee.AddToWatchlistAsync([mediaId], new ManualOrigin(ManualSource.Unknown));

		outbox.DropAsync(Any<IEnumerable<MediaId>>(), Any()).WasCalled(Times.Once);
		repository.AddAsync(Any<IEnumerable<MediaId>>(), Any<WatchlistItemProvenance>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task AddToWatchlistAsync_PublishesItemsAddedForTheEffectiveDelta()
	{
		var mediaId = MediaId.From(Guid.NewGuid());

		var repository = IWatchlistRepository.Mock();
		repository.GetIdsAsync(Any()).Returns([]);
		repository.AddAsync(Any<IEnumerable<MediaId>>(), Any<WatchlistItemProvenance>(), Any()).Returns([mediaId]);

		var outbox = IWatchlistOutbox.Mock();
		outbox.GetAsync(Any()).Returns([new WatchlistOutboxEntry(mediaId, WatchlistOperationAction.Remove, WatchlistOutboxState.Pending, 0)]);

		var publisher = new RecordingPublisher();
		var testee = CreateTestee(repository, outbox, IMediaService.Mock().Object, publisher);

		await testee.AddToWatchlistAsync([mediaId], new ManualOrigin(ManualSource.Search));

		var events = publisher.Published.OfType<WatchlistItemsAddedEvent>().ToList();
		await Assert.That(events.Count).IsEqualTo(1);
		await Assert.That(events[0].MediaIds).Contains(mediaId);
	}

	[Test]
	public async Task AddToWatchlistAsync_WithNoEffectiveDelta_DoesNotPublish()
	{
		var mediaId = MediaId.From(Guid.NewGuid());

		var repository = IWatchlistRepository.Mock();
		repository.GetIdsAsync(Any()).Returns([]);
		repository.AddAsync(Any<IEnumerable<MediaId>>(), Any<WatchlistItemProvenance>(), Any()).Returns([]);

		var outbox = IWatchlistOutbox.Mock();
		outbox.GetAsync(Any()).Returns([new WatchlistOutboxEntry(mediaId, WatchlistOperationAction.Remove, WatchlistOutboxState.Pending, 0)]);

		var publisher = new RecordingPublisher();
		var testee = CreateTestee(repository, outbox, IMediaService.Mock().Object, publisher);

		await testee.AddToWatchlistAsync([mediaId], new ManualOrigin(ManualSource.Search));

		await Assert.That(publisher.Published).IsEmpty();
	}

	[Test]
	public async Task RemoveFromWatchlistAsync_PublishesItemsRemovedForTheEffectiveDelta()
	{
		var mediaId = MediaId.From(Guid.NewGuid());

		var repository = IWatchlistRepository.Mock();
		repository.RemoveAsync(Any<IEnumerable<MediaId>>(), Any()).Returns([mediaId]);

		var outbox = IWatchlistOutbox.Mock();
		outbox.GetAsync(Any()).Returns([new WatchlistOutboxEntry(mediaId, WatchlistOperationAction.Add, WatchlistOutboxState.Pending, 0)]);

		var publisher = new RecordingPublisher();
		var testee = CreateTestee(repository, outbox, IMediaService.Mock().Object, publisher);

		await testee.RemoveFromWatchlistAsync([mediaId]);

		var events = publisher.Published.OfType<WatchlistItemsRemovedEvent>().ToList();
		await Assert.That(events.Count).IsEqualTo(1);
		await Assert.That(events[0].MediaIds).Contains(mediaId);
	}

	private static WatchlistService CreateTestee(
		Mock<IWatchlistRepository> repository,
		Mock<IWatchlistOutbox> outbox,
		IMediaService media,
		IMessagePublisher? publisher = null
	)
	{
		var mediaRepository = IMediaRepository.Mock();
		var configRepository = IProviderConfigRepository.Mock();
		var clock = IDateTimeProvider.Mock();
		clock.Now.Getter.Returns(DateTimeOffset.UnixEpoch);
		return new WatchlistService(
			repository.Object,
			outbox.Object,
			media,
			mediaRepository.Object,
			configRepository.Object,
			publisher ?? new RecordingPublisher(),
			new TestCurrentUser(),
			clock.Object
		);
	}

	private sealed class RecordingPublisher : IMessagePublisher
	{
		public List<object> Published { get; } = [];

		public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
			where TMessage : notnull
		{
			Published.Add(message);
			return Task.CompletedTask;
		}
	}

	private static async IAsyncEnumerable<T> Async<T>(params T[] items)
	{
		foreach (var item in items)
		{
			yield return item;
			await Task.CompletedTask;
		}
	}

	private Movie CreateMovie(MediaId mediaId) =>
		new()
		{
			Identity = new MediaIdentity(mediaId, [PlexId.From(_faker.Random.AlphaNumeric(10), MediaType.Movie)]),
			Title = _faker.Lorem.Sentence(2),
			Tagline = _faker.Company.CatchPhrase(),
			Summary = _faker.Lorem.Paragraph(),
			PosterUrl = _faker.Internet.Url(),
			BackdropUrl = _faker.Internet.Url(),
			Runtime = TimeSpan.FromMinutes(_faker.Random.Int(60, 180)),
			ReleaseDate = _faker.Date.PastDateOnly(),
			Genres = [],
		};
}
