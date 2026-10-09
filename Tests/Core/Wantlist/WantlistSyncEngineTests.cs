using Contracts.Identity;
using Contracts.Messaging;
using Contracts.Wantlist;
using Core.Wantlist.Sync;
using Domain.Identity;
using Domain.Media;
using Domain.Notifications;
using Domain.Wantlist.Arr;
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
	public async Task SyncAsync_WhenSomePushesFail_PublishesOneEventCountingWhatTheSinkAcked()
	{
		var delivered = MediaId.New();
		var rejected = MediaId.New();
		var stranded = MediaId.New();
		var sink = WantlistStubs.SinkAcking(
			new SinkSyncAck(delivered, SinkSyncOutcome.Applied),
			new SinkSyncAck(rejected, SinkSyncOutcome.Failed),
			new SinkSyncAck(stranded, SinkSyncOutcome.Unaddressable)
		);
		var published = new RecordingPublisher();

		await BuildEngine(sink, publisher: published, pending: [delivered, rejected, stranded]).SyncAsync(new WantlistSyncTrigger(new ManualTrigger()));

		var events = published.Published<WantlistSyncCompletedNotification>();
		await Assert.That(events).Count().IsEqualTo(1);
		await Assert.That(events[0].PushedAdds).IsEquivalentTo([delivered]);
		await Assert.That(events[0].FailedPushes).IsEquivalentTo([rejected, stranded]);
		await Assert.That(published.Published<WantlistSyncFailedNotification>()).IsEmpty();
	}

	[Test]
	public async Task SyncAsync_WhileSonarrIsDown_PushesTheMoviesAndStillReportsTheSyncFailedForSonarr()
	{
		var (movie, show) = (MediaId.New(), MediaId.New());
		var sink = WantlistStubs.SinkAcking(new SinkSyncAck(movie, SinkSyncOutcome.Applied));
		sink.GetAsync(Any()).Returns(new SinkSnapshot([], []) { Down = Some.Of(new WantlistSyncSinkUnreachable("Sonarr is down.", [ArrApp.Sonarr])) });
		var published = new RecordingPublisher();
		var engine = WantlistStubs.Engine(
			sink,
			WantlistStubs.OutboxPending([movie, show]),
			user: User,
			publisher: published,
			media: WantlistStubs.Typed((movie, MediaType.Movie), (show, MediaType.Show))
		);

		await engine.SyncAsync(new WantlistSyncTrigger(new ManualTrigger()));

		sink.ApplyAsync(Any(), Is<IReadOnlyList<SinkPush>>(pushes => pushes!.Select(push => push.MediaId).SequenceEqual(new[] { movie })), Any())
			.WasCalled(Times.Once);
		sink.ApplyAsync(Any(), Is<IReadOnlyList<SinkPush>>(pushes => pushes!.Any(push => push.MediaId == show)), Any()).WasCalled(Times.Never);
		await Assert.That(published.Published<WantlistSyncFailedNotification>().Select(failed => failed.Reason)).IsEquivalentTo(["Sonarr is down."]);
		await Assert.That(published.Published<WantlistSyncCompletedNotification>().Single().PushedAdds).IsEquivalentTo([movie]);
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
