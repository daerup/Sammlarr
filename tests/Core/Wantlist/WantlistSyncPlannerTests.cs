using Contracts.Wantlist;
using Contracts.Wantlist.Outbox;
using Core.Wantlist.Sync;
using Domain.Media;
using Domain.Usage;
using Domain.Wantlist.Arr;
using Domain.Wantlist.Outbox;
using Domain.Wantlist.Sync;
using Microsoft.Extensions.Logging;
using Observability.Usage;
using TestInfrastructure;

namespace Core.Tests.Wantlist;

public class WantlistSyncPlannerTests
{
	[Test]
	public async Task PlanAsync_ReadsTheSinkUnderTheWantlistSyncCause()
	{
		var cause = UsageCause.Other;
		var sink = WantlistStubs.EmptySink();
		sink.GetAsync(Any()).Returns(WantlistStubs.NothingOnTheSink).Callback(() => cause = UsageCauses.Current.Cause);
		var outbox = IWantlistOutbox.Mock();
		outbox.GetAllAsync(Any(), Any()).Returns([]);

		await WantlistStubs.Planner(sink, outbox).PlanAsync();

		await Assert.That(cause).IsEqualTo(UsageCause.WantlistSync);
	}

	[Test]
	public async Task PlanAsync_WhenAQueuedRemovalTheSinkAlreadyAgreesWith_DropsItWithoutRunningASync()
	{
		var stale = MediaId.From(new Guid(11, 0, 0, new byte[8]));
		var outbox = IWantlistOutbox.Mock();
		outbox.GetAllAsync(Any(), Any()).Returns([new WantlistOutboxEntry(stale, WantlistOperationAction.Remove, WantlistOutboxState.Pending, 0)]);

		await WantlistStubs.Planner(WantlistStubs.EmptySink(), outbox).PlanAsync();

		outbox.DropAsync(Any(), Any<IEnumerable<MediaId>>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task PlanAsync_WhenTheOutboxAgreesWithTheSink_LeavesItAlone()
	{
		var outbox = WantlistStubs.EmptyOutbox();

		await WantlistStubs.Planner(WantlistStubs.EmptySink(), outbox).PlanAsync();

		outbox.DropAsync(Any(), Any<IEnumerable<MediaId>>(), Any()).WasNeverCalled();
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

	[Test]
	public async Task PlanAsync_WhenBothArrAppsCannotBeRead_ReportsBoth()
	{
		var sink = IMediaSink.Mock();
		sink.GetAsync(Any()).Throws(new ArrAppsUnreachableException("Radarr is down. Sonarr is down.", [ArrApp.Radarr, ArrApp.Sonarr]));

		var result = await WantlistStubs.Planner(sink, WantlistStubs.EmptyOutbox()).PlanAsync();

		var unreachable = await Assert.That(result).IsOfUnionType<WantlistPlanResult, WantlistSyncSinkUnreachable>();
		await Assert.That(unreachable.Apps).IsEquivalentTo([ArrApp.Radarr, ArrApp.Sonarr]);
	}

	[Test]
	public async Task PlanAsync_WhenOnlySonarrCannotBeRead_NeitherDropsNorPlansAQueuedShowRemoval()
	{
		var show = MediaId.New();
		var sink = IMediaSink.Mock();
		sink.GetAsync(Any()).Returns(new SinkSnapshot([], []) { Down = Some.Of(new WantlistSyncSinkUnreachable("Sonarr is down.", [ArrApp.Sonarr])) });
		var outbox = IWantlistOutbox.Mock();
		outbox.GetAllAsync(Any(), Any()).Returns([new WantlistOutboxEntry(show, WantlistOperationAction.Remove, WantlistOutboxState.Pending, 0)]);

		var result = await WantlistStubs.Planner(sink, outbox, media: WantlistStubs.Typed((show, MediaType.Show))).PlanAsync();

		var summary = await Assert.That(result).IsOfUnionType<WantlistPlanResult, WantlistSyncSummary>();
		await Assert.That(summary!.Push.Removes).IsEmpty();
		await Assert.That(summary.Waiting).IsEquivalentTo([show]);
		outbox.DropAsync(Any(), Any<IEnumerable<MediaId>>(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task PlanAsync_WhenOnlySonarrCannotBeRead_HoldsBackATitleWhoseTypeIsUnknown()
	{
		var unknown = MediaId.New();
		var sink = IMediaSink.Mock();
		sink.GetAsync(Any()).Returns(new SinkSnapshot([], []) { Down = Some.Of(new WantlistSyncSinkUnreachable("Sonarr is down.", [ArrApp.Sonarr])) });
		var repository = WantlistStubs.EmptyRepository();
		repository.GetAllAsync(Any()).Returns([.. WantlistStubs.Wanted([unknown]).Items]);

		var result = await WantlistStubs
			.Planner(sink, WantlistStubs.EmptyOutbox(), repository: repository, media: WantlistStubs.Typed((unknown, MediaType.Unknown)))
			.PlanAsync();

		var summary = await Assert.That(result).IsOfUnionType<WantlistPlanResult, WantlistSyncSummary>();
		await Assert.That(summary!.Pull.Removes).IsEmpty();
	}

	[Test]
	public async Task PlanAsync_WhenCancelledWhileTheSinkIsRead_StopsBeforeReadingTheWantlist()
	{
		using var leaving = new CancellationTokenSource();
		var sink = IMediaSink.Mock();
		sink.GetAsync(Any()).Returns(WantlistStubs.NothingOnTheSink).Callback(leaving.Cancel);
		var repository = WantlistStubs.EmptyRepository();

		await Assert
			.That(async () => await WantlistStubs.Planner(sink, WantlistStubs.EmptyOutbox(), repository: repository).PlanAsync(leaving.Token))
			.Throws<OperationCanceledException>();
		repository.GetAllAsync(Any()).WasNeverCalled();
	}

	[Test]
	public async Task PlanAsync_WhenTheSinkCannotBeRead_WarnsWhy()
	{
		var sink = IMediaSink.Mock();
		sink.GetAsync(Any()).Throws(new HttpRequestException("plex is down"));
		var logger = ILogger<WantlistSyncPlanner>.Mock();

		await WantlistStubs.Planner(sink, WantlistStubs.EmptyOutbox(), logger.Object).PlanAsync();

		await Assert
			.That(Mock.Invocations(logger).Where(call => call.Arguments[0] is LogLevel.Warning).Select(call => $"{call.Arguments[2]}"))
			.IsEquivalentTo(["Reading Plex failed: plex is down"]);
	}
}
