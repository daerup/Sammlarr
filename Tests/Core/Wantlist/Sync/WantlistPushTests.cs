using Contracts.Announcing;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Wantlist;
using Contracts.Wantlist.Outbox;
using Domain.Identity;
using Domain.Media;
using Domain.Wantlist.Outbox;
using Domain.Wantlist.Sync;
using TestInfrastructure;

namespace Core.Tests.Wantlist.Sync;

public class WantlistPushTests
{
	private static readonly ICurrentUser Owner = new TestCurrentUser();

	[Test]
	public async Task SyncAsync_WhenAPendingAddPushesSuccessfully_DropsItAndAnnouncesPushed()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var external = PlexId.From("plex-1", MediaType.Movie);
		var outbox = OutboxWith(Pending(mediaId, WantlistOperationAction.Add));
		var provider = ProviderPushing(external, success: true);
		var events = IEventPublisher.Mock();

		await Sync(provider, outbox, Externals(mediaId, external), events);

		provider.AddToWantlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).WasCalled(Times.Once);
		outbox.DropAsync(Any<IEnumerable<MediaId>>(), Any()).WasCalled(Times.Once);
		outbox.RecordFailedAttemptAsync(Any<IEnumerable<MediaId>>(), Any<WantlistOutboxState>(), Any()).WasCalled(Times.Never);
		events.Publish(Event(mediaId, WantlistOutboxEventKind.Pushed)).WasCalled(Times.Once);
	}

	[Test]
	public async Task SyncAsync_WhenAnEntryHasAlreadyFailed_LeavesItAloneAndPushesNothing()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var external = PlexId.From("plex-2", MediaType.Movie);
		var outbox = OutboxWith(new WantlistOutboxEntry(mediaId, WantlistOperationAction.Add, WantlistOutboxState.Failed, 5));
		var provider = ProviderPushing(external, success: true);

		await Sync(provider, outbox, Externals(mediaId, external));

		provider.AddToWantlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).WasCalled(Times.Never);
		outbox.DropAsync(Any<IEnumerable<MediaId>>(), Any()).WasCalled(Times.Never);
		outbox.RecordFailedAttemptAsync(Any<IEnumerable<MediaId>>(), Any<WantlistOutboxState>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task SyncAsync_WhenNothingIsPending_MakesNoProviderPush()
	{
		var provider = ProviderPushing(PlexId.From("plex-0", MediaType.Movie), success: true);

		await Sync(provider, WantlistStubs.EmptyOutbox(), Externals());

		provider.AddToWantlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).WasCalled(Times.Never);
		provider.RemoveFromWantlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task SyncAsync_WhenTheEntryHasNoExternalId_AbandonsItWithoutPushing()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var outbox = OutboxWith(Pending(mediaId, WantlistOperationAction.Add));
		var provider = ProviderPushing(PlexId.From("plex-0", MediaType.Movie), success: true);
		var events = IEventPublisher.Mock();

		await Sync(provider, outbox, Externals(), events);

		provider.AddToWantlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).WasCalled(Times.Never);
		outbox.RecordFailedAttemptAsync(Any<IEnumerable<MediaId>>(), WantlistOutboxState.Failed, Any()).WasCalled(Times.Once);
		events.Publish(Event(mediaId, WantlistOutboxEventKind.Abandoned)).WasCalled(Times.Once);
	}

	[Test]
	public async Task SyncAsync_WhenAPushFailsBelowMaxAttempts_KeepsItPendingAndAnnouncesRetrying()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var external = PlexId.From("plex-3", MediaType.Movie);
		var outbox = OutboxWith(Pending(mediaId, WantlistOperationAction.Remove));
		var provider = ProviderPushing(external, success: false);
		var events = IEventPublisher.Mock();

		await Sync(provider, outbox, Externals(mediaId, external), events);

		outbox.RecordFailedAttemptAsync(Any<IEnumerable<MediaId>>(), WantlistOutboxState.Pending, Any()).WasCalled(Times.Once);
		events.Publish(Event(mediaId, WantlistOutboxEventKind.Retrying)).WasCalled(Times.Once);
	}

	[Test]
	public async Task SyncAsync_WhenAPushFailsAtMaxAttempts_MarksItFailedAndAnnouncesAbandoned()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var external = PlexId.From("plex-4", MediaType.Movie);
		var outbox = OutboxWith(new WantlistOutboxEntry(mediaId, WantlistOperationAction.Add, WantlistOutboxState.Pending, 4));
		var provider = ProviderPushing(external, success: false);
		var events = IEventPublisher.Mock();

		await Sync(provider, outbox, Externals(mediaId, external), events);

		outbox.RecordFailedAttemptAsync(Any<IEnumerable<MediaId>>(), WantlistOutboxState.Failed, Any()).WasCalled(Times.Once);
		outbox.DropAsync(Any<IEnumerable<MediaId>>(), Any()).WasCalled(Times.Never);
		events.Publish(Event(mediaId, WantlistOutboxEventKind.Abandoned)).WasCalled(Times.Once);
	}

	[Test]
	public async Task SyncAsync_WhenOnePushSucceedsAndAnotherHasNoExternalId_AnnouncesBoth()
	{
		var pushable = MediaId.From(Guid.NewGuid());
		var stranded = MediaId.From(Guid.NewGuid());
		var external = PlexId.From("plex-5", MediaType.Movie);
		var outbox = OutboxWith(Pending(pushable, WantlistOperationAction.Add), Pending(stranded, WantlistOperationAction.Add));
		var provider = ProviderPushing(external, success: true);
		var events = IEventPublisher.Mock();

		await Sync(provider, outbox, Externals(pushable, external), events);

		events.Publish(Event(pushable, WantlistOutboxEventKind.Pushed)).WasCalled(Times.Once);
		events.Publish(Event(stranded, WantlistOutboxEventKind.Abandoned)).WasCalled(Times.Once);
	}

	[Test]
	public async Task SyncAsync_WhenSeveralEntriesShareAnAction_SendsThemAsOneProviderCall()
	{
		var first = MediaId.From(Guid.NewGuid());
		var second = MediaId.From(Guid.NewGuid());
		var firstExternal = PlexId.From("plex-6", MediaType.Movie);
		var secondExternal = PlexId.From("plex-7", MediaType.Movie);
		var outbox = OutboxWith(Pending(first, WantlistOperationAction.Add), Pending(second, WantlistOperationAction.Add));
		var provider = ProviderAcking(new SinkSyncAck(firstExternal, true), new SinkSyncAck(secondExternal, true));
		var events = IEventPublisher.Mock();

		await Sync(provider, outbox, new Dictionary<MediaId, IExternalMediaId> { [first] = firstExternal, [second] = secondExternal }, events);

		provider.AddToWantlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).WasCalled(Times.Once);
		events.Publish(Event(first, WantlistOutboxEventKind.Pushed)).WasCalled(Times.Once);
		events.Publish(Event(second, WantlistOutboxEventKind.Pushed)).WasCalled(Times.Once);
	}

	[Test]
	public async Task SyncAsync_WhenTheProviderAcksFewerThanItWasSent_TreatsTheSilentOnesAsFailures()
	{
		var acked = MediaId.From(Guid.NewGuid());
		var ignored = MediaId.From(Guid.NewGuid());
		var ackedExternal = PlexId.From("plex-8", MediaType.Movie);
		var ignoredExternal = PlexId.From("plex-9", MediaType.Movie);
		var outbox = OutboxWith(Pending(acked, WantlistOperationAction.Add), Pending(ignored, WantlistOperationAction.Add));
		var provider = ProviderAcking(new SinkSyncAck(ackedExternal, true));
		var events = IEventPublisher.Mock();

		await Sync(provider, outbox, new Dictionary<MediaId, IExternalMediaId> { [acked] = ackedExternal, [ignored] = ignoredExternal }, events);

		events.Publish(Event(acked, WantlistOutboxEventKind.Pushed)).WasCalled(Times.Once);
		events.Publish(Event(ignored, WantlistOutboxEventKind.Retrying)).WasCalled(Times.Once);
	}

	private static Mock<IMediaSink> ProviderAcking(params SinkSyncAck[] acks)
	{
		var provider = WantlistStubs.EmptySink();
		provider.AddToWantlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).Returns(Stream(acks));
		provider.RemoveFromWantlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).Returns(Stream(acks));
		return provider;
	}

	private static WantlistOutboxEvent Event(MediaId mediaId, WantlistOutboxEventKind kind) => new(Owner.Id, mediaId, kind);

	private static WantlistOutboxEntry Pending(MediaId mediaId, WantlistOperationAction action) => new(mediaId, action, WantlistOutboxState.Pending, 0);

	private static Mock<IWantlistOutbox> OutboxWith(params WantlistOutboxEntry[] entries)
	{
		var outbox = IWantlistOutbox.Mock();
		outbox.GetAsync(Any()).Returns(entries);
		return outbox;
	}

	private static Mock<IMediaSink> ProviderPushing(IExternalMediaId external, bool success)
	{
		var provider = WantlistStubs.EmptySink();
		provider.AddToWantlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).Returns(Stream(new SinkSyncAck(external, success)));
		provider.RemoveFromWantlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).Returns(Stream(new SinkSyncAck(external, success)));
		return provider;
	}

	private static async Task Sync(
		Mock<IMediaSink> provider,
		Mock<IWantlistOutbox> outbox,
		IReadOnlyDictionary<MediaId, IExternalMediaId> externals,
		Mock<IEventPublisher>? events = null
	) => await WantlistStubs.Engine(provider, outbox, externals, events, user: Owner).SyncAsync(new WantlistSyncTrigger(new ManualTrigger()));

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
