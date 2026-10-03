using Contracts.Wantlist;
using Contracts.Wantlist.Outbox;
using Domain.Media;
using Domain.Wantlist.Outbox;
using Domain.Wantlist.Sync;
using TestInfrastructure;

namespace Core.Tests.Wantlist;

public class WantlistSyncPlannerTests
{
	[Test]
	public async Task PlanAsync_WhenAQueuedRemovalTheSinkAlreadyAgreesWith_DropsItWithoutRunningASync()
	{
		var stale = MediaId.From(new Guid(11, 0, 0, new byte[8]));
		var outbox = IWantlistOutbox.Mock();
		outbox.GetAllAsync(Any()).Returns([new WantlistOutboxEntry(stale, WantlistOperationAction.Remove, WantlistOutboxState.Pending, 0)]);

		await WantlistStubs.Planner(WantlistStubs.EmptySink(), outbox).PlanAsync();

		outbox.DropAsync(Any<IEnumerable<MediaId>>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task PlanAsync_WhenTheOutboxAgreesWithTheSink_LeavesItAlone()
	{
		var outbox = WantlistStubs.EmptyOutbox();

		await WantlistStubs.Planner(WantlistStubs.EmptySink(), outbox).PlanAsync();

		outbox.DropAsync(Any<IEnumerable<MediaId>>(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task PlanAsync_WhenTheSinkHoldsAMatchedAndAnUnmatchedItem_PullsTheMatchedOneAndReportsTheOther()
	{
		var matched = MediaId.New();
		var unmatched = new UnmatchedMedia([PlexId.From("plex-1", MediaType.Movie)], new MovieDetails { Title = "Solaris" });
		var sink = IMediaSink.Mock();
		sink.GetAsync(Any()).Returns(new SinkSnapshot([matched], [unmatched]));

		var result = await WantlistStubs.Planner(sink, WantlistStubs.EmptyOutbox()).PlanAsync();

		var summary = await Assert.That(result).IsOfUnionType<WantlistPlanResult, WantlistSyncSummary>();
		await Assert.That(summary!.Pull.Adds).IsEquivalentTo([matched]);
		await Assert.That(summary.Unmatched).IsEquivalentTo([unmatched]);
	}

	[Test]
	public async Task PlanAsync_WhenTheSinkCannotBeRead_ReportsItUnreachable()
	{
		var sink = IMediaSink.Mock();
		sink.GetAsync(Any()).Throws(new HttpRequestException("plex is down"));

		var result = await WantlistStubs.Planner(sink, WantlistStubs.EmptyOutbox()).PlanAsync();

		var unreachable = await Assert.That(result).IsOfUnionType<WantlistPlanResult, WantlistSyncSinkUnreachable>();
		await Assert.That(unreachable.Reason).Contains("plex is down");
	}
}
