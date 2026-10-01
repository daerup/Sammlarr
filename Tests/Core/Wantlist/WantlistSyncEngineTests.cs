using Contracts.Identity;
using Contracts.Messaging;
using Contracts.Wantlist;
using Core.Wantlist.Sync;
using Domain.Identity;
using Domain.Media;
using Domain.Notifications;
using Domain.Wantlist.Sync;
using TestInfrastructure;

namespace Core.Tests.Wantlist;

public class WantlistSyncEngineTests
{
	private static readonly ICurrentUser User = new TestCurrentUser();
	private static readonly ICurrentUser Other = new TestCurrentUser { Id = UserId.From(new Guid("22222222-2222-2222-2222-222222222222")) };

	[Test]
	public async Task SyncAsync_OverlappingCalls_ReturnsAlreadyRunning()
	{
		var held = new TaskCompletionSource<SinkSnapshot>();
		var sink = IMediaSink.Mock();
		sink.GetAsync(Any()).ReturnsAsync(held.Task);
		var engine = BuildEngine(sink);

		var first = engine.SyncAsync(new WantlistSyncTrigger(new ManualTrigger()));
		await Task.Delay(50);
		var second = await engine.SyncAsync(new WantlistSyncTrigger(new ManualTrigger()));

		await Assert.That(second).IsOfUnionType<WantlistSyncResult, WantlistSyncAlreadyRunning>();

		held.SetResult(WantlistStubs.NothingOnTheSink);
		await Assert.That(await first).IsOfUnionType<WantlistSyncResult, WantlistSyncSummary>();
	}

	[Test]
	public async Task SyncAsync_WhileAnotherUserIsSyncing_RunsAnyway()
	{
		var held = new TaskCompletionSource<SinkSnapshot>();
		var busy = IMediaSink.Mock();
		busy.GetAsync(Any()).ReturnsAsync(held.Task);
		var idle = WantlistStubs.EmptySink();
		var shared = new WantlistSyncGate();

		var blocking = BuildEngine(busy, shared).SyncAsync(new WantlistSyncTrigger(new ManualTrigger()));
		await Task.Delay(50);
		var other = await BuildEngine(idle, shared, Other).SyncAsync(new WantlistSyncTrigger(new ManualTrigger()));

		await Assert.That(other).IsOfUnionType<WantlistSyncResult, WantlistSyncSummary>();

		held.SetResult(WantlistStubs.NothingOnTheSink);
		await blocking;
	}

	[Test]
	public async Task SyncAsync_WhenPushesFailMidBatch_PublishesOneEventNamingTheFailures()
	{
		var stranded = MediaId.From(Guid.NewGuid());
		var alsoStranded = MediaId.From(Guid.NewGuid());
		var sink = WantlistStubs.SinkAcking(new SinkSyncAck(stranded, SinkSyncOutcome.Unaddressable), new SinkSyncAck(alsoStranded, SinkSyncOutcome.Unaddressable));
		var published = new RecordingPublisher();

		await BuildEngine(sink, publisher: published, pending: [stranded, alsoStranded]).SyncAsync(new WantlistSyncTrigger(new ManualTrigger()));

		var events = published.Published<WantlistSyncCompletedNotification>();
		await Assert.That(events).Count().IsEqualTo(1);
		await Assert.That(events[0].FailedPushes).Contains(stranded).And.Contains(alsoStranded);
		await Assert.That(events[0].PushedAdds).IsEmpty();
	}

	private static WantlistSyncEngine BuildEngine(
		Mock<IMediaSink> sink,
		WantlistSyncGate? gate = null,
		ICurrentUser? user = null,
		IMessagePublisher? publisher = null,
		IReadOnlyList<MediaId>? pending = null
	) =>
		WantlistStubs.Engine(
			sink,
			pending is null ? WantlistStubs.EmptyOutbox() : WantlistStubs.OutboxPending(pending),
			gate: gate,
			user: user ?? User,
			publisher: publisher
		);
}
