using Contracts.Identity;
using Contracts.Media;
using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Domain.Identity;
using Domain.Media;
using Domain.Watchlist.Outbox;
using Domain.Watchlist.Sync;
using TestInfrastructure;

namespace Core.Tests.Watchlist.Sync;

public class WatchlistPushTests
{
	private static readonly ICurrentUser Owner = new TestCurrentUser();

	[Test]
	public async Task SyncAsync_WhenAPendingAddPushesSuccessfully_DropsItAndAnnouncesPushed()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var external = PlexId.From("plex-1", MediaType.Movie);
		var outbox = OutboxWith(Pending(mediaId, WatchlistOperationAction.Add));
		var provider = ProviderPushing(external, success: true);
		var feed = IWatchlistOutboxEventFeed.Mock();

		await Sync(provider, outbox, Externals(mediaId, external), feed);

		provider.AddToWatchlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).WasCalled(Times.Once);
		outbox.DropAsync(Any<IEnumerable<MediaId>>(), Any()).WasCalled(Times.Once);
		outbox.RecordFailedAttemptAsync(Any<IEnumerable<MediaId>>(), Any<WatchlistOutboxState>(), Any()).WasCalled(Times.Never);
		feed.Publish(Event(mediaId, WatchlistOutboxEventKind.Pushed)).WasCalled(Times.Once);
	}

	[Test]
	public async Task SyncAsync_WhenAnEntryHasAlreadyFailed_LeavesItAloneAndPushesNothing()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var external = PlexId.From("plex-2", MediaType.Movie);
		var outbox = OutboxWith(new WatchlistOutboxEntry(mediaId, WatchlistOperationAction.Add, WatchlistOutboxState.Failed, 5));
		var provider = ProviderPushing(external, success: true);

		await Sync(provider, outbox, Externals(mediaId, external));

		provider.AddToWatchlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).WasCalled(Times.Never);
		outbox.DropAsync(Any<IEnumerable<MediaId>>(), Any()).WasCalled(Times.Never);
		outbox.RecordFailedAttemptAsync(Any<IEnumerable<MediaId>>(), Any<WatchlistOutboxState>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task SyncAsync_WhenNothingIsPending_MakesNoProviderPush()
	{
		var provider = ProviderPushing(PlexId.From("plex-0", MediaType.Movie), success: true);

		await Sync(provider, WatchlistStubs.EmptyOutbox(), Externals());

		provider.AddToWatchlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).WasCalled(Times.Never);
		provider.RemoveFromWatchlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task SyncAsync_WhenTheEntryHasNoExternalId_AbandonsItWithoutPushing()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var outbox = OutboxWith(Pending(mediaId, WatchlistOperationAction.Add));
		var provider = ProviderPushing(PlexId.From("plex-0", MediaType.Movie), success: true);
		var feed = IWatchlistOutboxEventFeed.Mock();

		await Sync(provider, outbox, Externals(), feed);

		provider.AddToWatchlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).WasCalled(Times.Never);
		outbox.RecordFailedAttemptAsync(Any<IEnumerable<MediaId>>(), WatchlistOutboxState.Failed, Any()).WasCalled(Times.Once);
		feed.Publish(Event(mediaId, WatchlistOutboxEventKind.Abandoned)).WasCalled(Times.Once);
	}

	[Test]
	public async Task SyncAsync_WhenAPushFailsBelowMaxAttempts_KeepsItPendingAndAnnouncesRetrying()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var external = PlexId.From("plex-3", MediaType.Movie);
		var outbox = OutboxWith(Pending(mediaId, WatchlistOperationAction.Remove));
		var provider = ProviderPushing(external, success: false);
		var feed = IWatchlistOutboxEventFeed.Mock();

		await Sync(provider, outbox, Externals(mediaId, external), feed);

		outbox.RecordFailedAttemptAsync(Any<IEnumerable<MediaId>>(), WatchlistOutboxState.Pending, Any()).WasCalled(Times.Once);
		feed.Publish(Event(mediaId, WatchlistOutboxEventKind.Retrying)).WasCalled(Times.Once);
	}

	[Test]
	public async Task SyncAsync_WhenAPushFailsAtMaxAttempts_MarksItFailedAndAnnouncesAbandoned()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var external = PlexId.From("plex-4", MediaType.Movie);
		var outbox = OutboxWith(new WatchlistOutboxEntry(mediaId, WatchlistOperationAction.Add, WatchlistOutboxState.Pending, 4));
		var provider = ProviderPushing(external, success: false);
		var feed = IWatchlistOutboxEventFeed.Mock();

		await Sync(provider, outbox, Externals(mediaId, external), feed);

		outbox.RecordFailedAttemptAsync(Any<IEnumerable<MediaId>>(), WatchlistOutboxState.Failed, Any()).WasCalled(Times.Once);
		outbox.DropAsync(Any<IEnumerable<MediaId>>(), Any()).WasCalled(Times.Never);
		feed.Publish(Event(mediaId, WatchlistOutboxEventKind.Abandoned)).WasCalled(Times.Once);
	}

	[Test]
	public async Task SyncAsync_WhenOnePushSucceedsAndAnotherHasNoExternalId_AnnouncesBoth()
	{
		var pushable = MediaId.From(Guid.NewGuid());
		var stranded = MediaId.From(Guid.NewGuid());
		var external = PlexId.From("plex-5", MediaType.Movie);
		var outbox = OutboxWith(Pending(pushable, WatchlistOperationAction.Add), Pending(stranded, WatchlistOperationAction.Add));
		var provider = ProviderPushing(external, success: true);
		var feed = IWatchlistOutboxEventFeed.Mock();

		await Sync(provider, outbox, Externals(pushable, external), feed);

		feed.Publish(Event(pushable, WatchlistOutboxEventKind.Pushed)).WasCalled(Times.Once);
		feed.Publish(Event(stranded, WatchlistOutboxEventKind.Abandoned)).WasCalled(Times.Once);
	}

	[Test]
	public async Task SyncAsync_WhenSeveralEntriesShareAnAction_SendsThemAsOneProviderCall()
	{
		var first = MediaId.From(Guid.NewGuid());
		var second = MediaId.From(Guid.NewGuid());
		var firstExternal = PlexId.From("plex-6", MediaType.Movie);
		var secondExternal = PlexId.From("plex-7", MediaType.Movie);
		var outbox = OutboxWith(Pending(first, WatchlistOperationAction.Add), Pending(second, WatchlistOperationAction.Add));
		var provider = ProviderAcking(new WatchlistPushAck(firstExternal, true), new WatchlistPushAck(secondExternal, true));
		var feed = IWatchlistOutboxEventFeed.Mock();

		await Sync(provider, outbox, new Dictionary<MediaId, IExternalMediaId> { [first] = firstExternal, [second] = secondExternal }, feed);

		provider.AddToWatchlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).WasCalled(Times.Once);
		feed.Publish(Event(first, WatchlistOutboxEventKind.Pushed)).WasCalled(Times.Once);
		feed.Publish(Event(second, WatchlistOutboxEventKind.Pushed)).WasCalled(Times.Once);
	}

	[Test]
	public async Task SyncAsync_WhenTheProviderAcksFewerThanItWasSent_TreatsTheSilentOnesAsFailures()
	{
		var acked = MediaId.From(Guid.NewGuid());
		var ignored = MediaId.From(Guid.NewGuid());
		var ackedExternal = PlexId.From("plex-8", MediaType.Movie);
		var ignoredExternal = PlexId.From("plex-9", MediaType.Movie);
		var outbox = OutboxWith(Pending(acked, WatchlistOperationAction.Add), Pending(ignored, WatchlistOperationAction.Add));
		var provider = ProviderAcking(new WatchlistPushAck(ackedExternal, true));
		var feed = IWatchlistOutboxEventFeed.Mock();

		await Sync(provider, outbox, new Dictionary<MediaId, IExternalMediaId> { [acked] = ackedExternal, [ignored] = ignoredExternal }, feed);

		feed.Publish(Event(acked, WatchlistOutboxEventKind.Pushed)).WasCalled(Times.Once);
		feed.Publish(Event(ignored, WatchlistOutboxEventKind.Retrying)).WasCalled(Times.Once);
	}

	private static Mock<IWatchlistProvider> ProviderAcking(params WatchlistPushAck[] acks)
	{
		var provider = IWatchlistProvider.Mock();
		provider.GetWatchlistAsync(Any()).Returns(WatchlistStubs.NothingRemote());
		provider.AddToWatchlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).Returns(Stream(acks));
		provider.RemoveFromWatchlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).Returns(Stream(acks));
		return provider;
	}

	private static WatchlistOutboxEvent Event(MediaId mediaId, WatchlistOutboxEventKind kind) => new(Owner.Id, mediaId, kind);

	private static WatchlistOutboxEntry Pending(MediaId mediaId, WatchlistOperationAction action) => new(mediaId, action, WatchlistOutboxState.Pending, 0);

	private static Mock<IWatchlistOutbox> OutboxWith(params WatchlistOutboxEntry[] entries)
	{
		var outbox = IWatchlistOutbox.Mock();
		outbox.GetAsync(Any()).Returns(entries);
		return outbox;
	}

	private static Mock<IWatchlistProvider> ProviderPushing(IExternalMediaId external, bool success)
	{
		var provider = IWatchlistProvider.Mock();
		provider.GetWatchlistAsync(Any()).Returns(WatchlistStubs.NothingRemote());
		provider.AddToWatchlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).Returns(Stream(new WatchlistPushAck(external, success)));
		provider.RemoveFromWatchlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).Returns(Stream(new WatchlistPushAck(external, success)));
		return provider;
	}

	private static async Task Sync(
		Mock<IWatchlistProvider> provider,
		Mock<IWatchlistOutbox> outbox,
		IReadOnlyDictionary<MediaId, IExternalMediaId> externals,
		Mock<IWatchlistOutboxEventFeed>? feed = null
	) => await WatchlistStubs.Engine(provider, outbox, externals, feed, user: Owner).SyncAsync(new WatchlistSyncTrigger(new ManualTrigger()));

	private static IReadOnlyDictionary<MediaId, IExternalMediaId> Externals(MediaId id, IExternalMediaId external) =>
		new Dictionary<MediaId, IExternalMediaId> { [id] = external };

	private static IReadOnlyDictionary<MediaId, IExternalMediaId> Externals() => new Dictionary<MediaId, IExternalMediaId>();

	private static async IAsyncEnumerable<T> Stream<T>(params T[] items)
	{
		foreach (var item in items)
		{
			yield return item;
		}

		await Task.CompletedTask;
	}
}
