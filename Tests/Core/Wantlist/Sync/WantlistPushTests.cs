using Contracts.Announcing;
using Contracts.Identity;
using Contracts.Wantlist;
using Contracts.Wantlist.Outbox;
using Domain.Media;
using Domain.Wantlist.Outbox;
using Domain.Wantlist.Sync;
using TestInfrastructure;

namespace Core.Tests.Wantlist.Sync;

public class WantlistPushTests
{
	private static readonly ICurrentUser Owner = new TestCurrentUser();
	private static readonly WantlistSyncTrigger Trigger = new(new ManualTrigger());

	[Test]
	public async Task SyncAsync_WhenAPendingAddPushesSuccessfully_DropsItAndAnnouncesPushed()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var outbox = OutboxWith(Pending(mediaId, WantlistOperationAction.Add));
		var sink = WantlistStubs.SinkAcking(new SinkSyncAck(mediaId, SinkSyncOutcome.Applied));
		var events = IEventPublisher.Mock();
		var testee = WantlistStubs.Engine(sink, outbox, events, user: Owner);

		await testee.SyncAsync(Trigger);

		sink.ApplyAsync(WantlistOperationAction.Add, Any(), Any()).WasCalled(Times.Once);
		outbox.DropAsync(Any<IEnumerable<MediaId>>(), Any()).WasCalled(Times.Once);
		outbox.RecordFailedAttemptAsync(Any<IEnumerable<MediaId>>(), Any<WantlistOutboxState>(), Any()).WasCalled(Times.Never);
		events.Publish(Event(mediaId, WantlistOutboxEventKind.Pushed)).WasCalled(Times.Once);
	}

	[Test]
	public async Task SyncAsync_WhenAnEntryHasAlreadyFailed_LeavesItAloneAndPushesNothing()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var outbox = OutboxWith(new WantlistOutboxEntry(mediaId, WantlistOperationAction.Add, WantlistOutboxState.Failed, 5));
		var sink = WantlistStubs.SinkAcking(new SinkSyncAck(mediaId, SinkSyncOutcome.Applied));
		var testee = WantlistStubs.Engine(sink, outbox, user: Owner);

		await testee.SyncAsync(Trigger);

		sink.ApplyAsync(Any(), Any(), Any()).WasCalled(Times.Never);
		outbox.DropAsync(Any<IEnumerable<MediaId>>(), Any()).WasCalled(Times.Never);
		outbox.RecordFailedAttemptAsync(Any<IEnumerable<MediaId>>(), Any<WantlistOutboxState>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task SyncAsync_WhenNothingIsPending_MakesNoSinkCall()
	{
		var sink = WantlistStubs.EmptySink();
		var testee = WantlistStubs.Engine(sink, WantlistStubs.EmptyOutbox(), user: Owner);

		await testee.SyncAsync(Trigger);

		sink.ApplyAsync(Any(), Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task SyncAsync_WhenTheSinkCannotAddressAnEntry_AbandonsItWithoutRetrying()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var outbox = OutboxWith(Pending(mediaId, WantlistOperationAction.Add));
		var events = IEventPublisher.Mock();
		var testee = WantlistStubs.Engine(WantlistStubs.SinkAcking(new SinkSyncAck(mediaId, SinkSyncOutcome.Unaddressable)), outbox, events, user: Owner);

		await testee.SyncAsync(Trigger);

		outbox.RecordFailedAttemptAsync(Any<IEnumerable<MediaId>>(), WantlistOutboxState.Failed, Any()).WasCalled(Times.Once);
		events.Publish(Event(mediaId, WantlistOutboxEventKind.Abandoned)).WasCalled(Times.Once);
	}

	[Test]
	public async Task SyncAsync_WhenAPushFailsBelowMaxAttempts_KeepsItPendingAndAnnouncesRetrying()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var outbox = OutboxWith(Pending(mediaId, WantlistOperationAction.Remove));
		var events = IEventPublisher.Mock();
		var testee = WantlistStubs.Engine(WantlistStubs.SinkAcking(new SinkSyncAck(mediaId, SinkSyncOutcome.Failed)), outbox, events, user: Owner);

		await testee.SyncAsync(Trigger);

		outbox.RecordFailedAttemptAsync(Any<IEnumerable<MediaId>>(), WantlistOutboxState.Pending, Any()).WasCalled(Times.Once);
		events.Publish(Event(mediaId, WantlistOutboxEventKind.Retrying)).WasCalled(Times.Once);
	}

	[Test]
	public async Task SyncAsync_WhenAPushFailsAtMaxAttempts_MarksItFailedAndAnnouncesAbandoned()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var outbox = OutboxWith(new WantlistOutboxEntry(mediaId, WantlistOperationAction.Add, WantlistOutboxState.Pending, 4));
		var events = IEventPublisher.Mock();
		var testee = WantlistStubs.Engine(WantlistStubs.SinkAcking(new SinkSyncAck(mediaId, SinkSyncOutcome.Failed)), outbox, events, user: Owner);

		await testee.SyncAsync(Trigger);

		outbox.RecordFailedAttemptAsync(Any<IEnumerable<MediaId>>(), WantlistOutboxState.Failed, Any()).WasCalled(Times.Once);
		outbox.DropAsync(Any<IEnumerable<MediaId>>(), Any()).WasCalled(Times.Never);
		events.Publish(Event(mediaId, WantlistOutboxEventKind.Abandoned)).WasCalled(Times.Once);
	}

	[Test]
	public async Task SyncAsync_WhenOnePushSucceedsAndTheSinkCannotAddressAnother_AnnouncesBoth()
	{
		var pushable = MediaId.From(Guid.NewGuid());
		var stranded = MediaId.From(Guid.NewGuid());
		var outbox = OutboxWith(Pending(pushable, WantlistOperationAction.Add), Pending(stranded, WantlistOperationAction.Add));
		var sink = WantlistStubs.SinkAcking(new SinkSyncAck(pushable, SinkSyncOutcome.Applied), new SinkSyncAck(stranded, SinkSyncOutcome.Unaddressable));
		var events = IEventPublisher.Mock();
		var testee = WantlistStubs.Engine(sink, outbox, events, user: Owner);

		await testee.SyncAsync(Trigger);

		events.Publish(Event(pushable, WantlistOutboxEventKind.Pushed)).WasCalled(Times.Once);
		events.Publish(Event(stranded, WantlistOutboxEventKind.Abandoned)).WasCalled(Times.Once);
	}

	[Test]
	public async Task SyncAsync_WhenSeveralEntriesShareAnAction_SendsThemAsOneSinkCall()
	{
		var first = MediaId.From(Guid.NewGuid());
		var second = MediaId.From(Guid.NewGuid());
		var outbox = OutboxWith(Pending(first, WantlistOperationAction.Add), Pending(second, WantlistOperationAction.Add));
		var sink = WantlistStubs.SinkAcking(new SinkSyncAck(first, SinkSyncOutcome.Applied), new SinkSyncAck(second, SinkSyncOutcome.Applied));
		var events = IEventPublisher.Mock();
		var testee = WantlistStubs.Engine(sink, outbox, events, user: Owner);

		await testee.SyncAsync(Trigger);

		sink.ApplyAsync(WantlistOperationAction.Add, Any(), Any()).WasCalled(Times.Once);
		events.Publish(Event(first, WantlistOutboxEventKind.Pushed)).WasCalled(Times.Once);
		events.Publish(Event(second, WantlistOutboxEventKind.Pushed)).WasCalled(Times.Once);
	}

	[Test]
	public async Task SyncAsync_WhenTheSinkAcksFewerThanItWasSent_TreatsTheSilentOnesAsFailures()
	{
		var acked = MediaId.From(Guid.NewGuid());
		var ignored = MediaId.From(Guid.NewGuid());
		var outbox = OutboxWith(Pending(acked, WantlistOperationAction.Add), Pending(ignored, WantlistOperationAction.Add));
		var events = IEventPublisher.Mock();
		var testee = WantlistStubs.Engine(WantlistStubs.SinkAcking(new SinkSyncAck(acked, SinkSyncOutcome.Applied)), outbox, events, user: Owner);

		await testee.SyncAsync(Trigger);

		events.Publish(Event(acked, WantlistOutboxEventKind.Pushed)).WasCalled(Times.Once);
		events.Publish(Event(ignored, WantlistOutboxEventKind.Retrying)).WasCalled(Times.Once);
	}

	private static WantlistOutboxEvent Event(MediaId mediaId, WantlistOutboxEventKind kind) => new(Owner.Id, mediaId, kind);

	private static WantlistOutboxEntry Pending(MediaId mediaId, WantlistOperationAction action) => new(mediaId, action, WantlistOutboxState.Pending, 0);

	private static Mock<IWantlistOutbox> OutboxWith(params WantlistOutboxEntry[] entries)
	{
		var outbox = IWantlistOutbox.Mock();
		outbox.GetAsync(Any()).Returns(entries);
		return outbox;
	}
}
