using Contracts;
using Contracts.Announcing;
using Contracts.Feed;
using Contracts.Identity;
using Contracts.Jobs;
using Contracts.Providers;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Feed.Trace;
using Domain.Identity;
using Domain.Jobs;
using Domain.Providers;
using TestInfrastructure;
using UI.Features.Feeds.Components;

namespace UI.Tests.Feeds;

public class FeedRunProgressTests
{
	private static readonly DateTimeOffset Start = new(2026, 10, 8, 18, 0, 0, TimeSpan.Zero);
	private static readonly UserId Owner = UserId.From(new Guid(21, 0, 0, new byte[8]));
	private static readonly Guid Run = new(22, 0, 0, new byte[8]);
	private static readonly Feed Documentaries = TestFeedRows.Documentaries.Overview.Feed;
	private static readonly Feed Comedy = TestFeedRows.Comedy.Overview.Feed;
	private static readonly RunLayout Layout = new([MediaProvider.Plex], [new(RemoteService.Tmdb, 1, false)], [], [new(new None(), RemoteService.Tmdb)]);

	private readonly Mock<IJobQueueView> _queue = IJobQueueView.Mock();
	private readonly Mock<IEventPublisher> _events = IEventPublisher.Mock();
	private readonly Mock<IFeedService> _feeds = IFeedService.Mock();
	private readonly Mock<IFeedRunLayout> _layouts = IFeedRunLayout.Mock();
	private readonly Mock<IFeedRunTraces> _traces = IFeedRunTraces.Mock();
	private readonly Mock<IRemoteCapacity> _capacity = IRemoteCapacity.Mock();
	private readonly Mock<ICurrentUser> _user = ICurrentUser.Mock();
	private readonly Mock<IDateTimeProvider> _clock = IDateTimeProvider.Mock();

	[Before(Test)]
	public void Setup()
	{
		_user.IsAuthenticated.Getter.Returns(true);
		_user.Id.Getter.Returns(Owner);
		_clock.Now.Getter.Returns(Start);
		_queue.GetAsync(Any()).Returns(new QueueSnapshot([], 0));
		_feeds.GetAsync(Any()).Returns(new FeedSnapshot([Overview(Documentaries), Overview(Comedy)]));
		_layouts.GetAsync(Any<FeedId>(), Any()).Returns(Layout);
		Reads(new NoTrace());
		_capacity.Of(Any<RemoteService>()).Returns(new RemoteLoad(new None(), 0));
	}

	[Test]
	public async Task Start_WhileYourFeedRuns_ReadsItsTrace()
	{
		Running(Documentaries);
		Reads(Snapshot(FateTally.None with { Started = 3, Matched = 1 }, new None()));
		using var testee = Progress();

		await testee.StartAsync();

		await EventuallyAsync(() => testee.Live.Match(run => run.Tally.Fates.Started == 3, () => false));
		var live = (await Assert.That(testee.Live).IsSome())!;
		await Assert.That(live.Feed).IsEqualTo(Documentaries);
		await Assert.That(live.Layout).IsEqualTo(Layout);
	}

	[Test]
	public async Task Start_WithoutARun_ReadsNothing()
	{
		using var testee = Progress();

		await testee.StartAsync();

		await Assert.That(testee.Live).IsNone();
		_traces.ReadAsync(Any<FeedId>(), Any<TraceCursor>(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task Total_BeforeTheSourceEnds_IsThePreviousRunsTotal()
	{
		Running(Documentaries);
		Reads(Snapshot(FateTally.None with { Started = 40, Matched = 30 }, new None()));
		using var testee = Progress();

		await testee.StartAsync();

		await EventuallyAsync(() => testee.Live.Match(run => run.Tally.Fates.Started == 40, () => false));
		var live = (await Assert.That(testee.Live).IsSome())!;
		var estimate = live.Tally;
		Reads(Snapshot(FateTally.None with { Started = 60, Matched = 50, ListedWithoutIds = 4 }, Some.Of(2000)));
		await EventuallyAsync(() => live.Tally.Fates.Started == 60);
		await Assert.That(estimate.Total).IsEqualTo(Some.Of(1999));
		await Assert.That(estimate.Estimated).IsTrue();
		await Assert.That(live.Tally.Total).IsEqualTo(Some.Of(2004));
		await Assert.That(live.Tally.Estimated).IsFalse();
	}

	[Test]
	public async Task Settle_WithoutATrace_FoldsAtOnce()
	{
		Running(Documentaries);
		using var testee = Progress();
		await testee.StartAsync();
		await EventuallyAsync(() => testee.Live.IsSome());
		var folded = false;
		(await Assert.That(testee.Live).IsSome())!.Folded += () => folded = true;

		_events.RaisePublished(Job(Documentaries, JobState.Done));

		await EventuallyAsync(() => folded && testee.Live.IsNone());
	}

	[Test]
	public async Task Settle_BeforeTheWatchStarts_StillFoldsAtOnce()
	{
		using var entered = new ManualResetEventSlim();
		using var release = new ManualResetEventSlim();
		_layouts.GetAsync(Any<FeedId>(), Any()).Returns(Layout).Callback(() => Hold(entered, release));
		using var testee = Progress();
		await testee.StartAsync();
		var changes = 0;
		testee.Changed += () => Interlocked.Increment(ref changes);

		var begun = Task.Run(() => _events.RaisePublished(Job(Documentaries, JobState.Running)));
		entered.Wait(TimeSpan.FromSeconds(5));
		_events.RaisePublished(Job(Documentaries, JobState.Done));
		release.Set();
		await begun;

		await EventuallyAsync(() => Volatile.Read(ref changes) >= 2 && testee.Live.IsNone());
	}

	[Test]
	public async Task Begin_WhenAnOlderRunLoadsLast_KeepsTheNewerRun()
	{
		using var entered = new ManualResetEventSlim();
		using var release = new ManualResetEventSlim();
		_layouts.GetAsync(Documentaries.Id, Any()).Returns(Layout).Callback(() => Hold(entered, release));
		using var testee = Progress();
		await testee.StartAsync();

		var older = Task.Run(() => _events.RaisePublished(Job(Documentaries, JobState.Running)));
		entered.Wait(TimeSpan.FromSeconds(5));
		_events.RaisePublished(Job(Comedy, JobState.Running));
		release.Set();
		await older;

		var live = (await Assert.That(testee.Live).IsSome())!;
		await Assert.That(live.Feed).IsEqualTo(Comedy);
	}

	[Test]
	public async Task Ended_FoldsFiveSecondsLater()
	{
		Running(Documentaries);
		Reads(Batch([], Some.Of(new FeedRunEnd(TraceEnding.Completed, 2000))));
		using var testee = Progress();
		await testee.StartAsync();
		await EventuallyAsync(() => testee.Live.Match(run => run.Tally.Ended.IsSome(), () => false));

		await Task.Delay(600);
		var lingering = testee.Live;
		_clock.Now.Getter.Returns(Start.AddSeconds(5));

		await EventuallyAsync(() => testee.Live.IsNone());
		await Assert.That(lingering).IsSome();
	}

	[Test]
	public async Task Hide_KeepsUntilTheNextRun()
	{
		Running(Documentaries);
		using var testee = Progress();
		await testee.StartAsync();
		await EventuallyAsync(() => testee.Live.IsSome());
		var first = (await Assert.That(testee.Live).IsSome())!;

		first.Hide();
		_events.RaisePublished(Job(Documentaries, JobState.Running));

		await EventuallyAsync(() => testee.Live.Match(run => run != first, () => false));
		var next = (await Assert.That(testee.Live).IsSome())!;
		await Assert.That(first.Hidden).IsTrue();
		await Assert.That(next.Hidden).IsFalse();
	}

	[Test]
	public async Task Start_ForAnotherUsersRun_ShowsNothing()
	{
		using var testee = Progress();
		await testee.StartAsync();

		_events.RaisePublished(new JobStateChangedEvent(UserId.From(Guid.NewGuid()), JobKind.FeedRun, Documentaries.Id.Value.ToString(), JobState.Running));
		await Task.Delay(300);

		await Assert.That(testee.Live).IsNone();
		_traces.ReadAsync(Any<FeedId>(), Any<TraceCursor>(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task Start_WhenSignedOut_ShowsNothing()
	{
		_user.IsAuthenticated.Getter.Returns(false);
		Running(Documentaries);
		using var testee = Progress();
		await testee.StartAsync();

		_events.RaisePublished(Job(Documentaries, JobState.Running));
		await Task.Delay(300);

		await Assert.That(testee.Live).IsNone();
		_traces.ReadAsync(Any<FeedId>(), Any<TraceCursor>(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task Batch_SendsFlatWireEntries()
	{
		var answered = new TraceStep(9, 1900, Some.Of(3), new HopAnswered(RemoteService.Tmdb, TraceStage.Ids, HopOutcome.AfterRateLimit, 820));
		Running(Documentaries);
		Reads(Batch([answered], new None()));
		_capacity.Of(Any<RemoteService>()).Returns(new RemoteLoad(Some.Of(Start.AddMilliseconds(1500)), 0.5));
		var batches = new List<WireBatch>();
		using var testee = Progress();
		await testee.StartAsync();
		await EventuallyAsync(() => testee.Live.IsSome());

		(await Assert.That(testee.Live).IsSome())!.Batch += batches.Add;

		await EventuallyAsync(() => batches.Count > 0);
		await Assert
			.That(batches[0].Steps)
			.IsEquivalentTo([
				new WireStep(9, 1900, 3, (byte)WireKind.Answered)
				{
					Service = (byte)RemoteService.Tmdb,
					Stage = (byte)TraceStage.Ids,
					Outcome = (byte)HopOutcome.AfterRateLimit,
					Ms = 820,
				},
			]);
		await Assert.That(batches[0].Capacity).IsEquivalentTo([new WireCapacity((byte)RemoteService.Tmdb, 3500, 0.5)]);
	}

	[Test]
	public async Task Batch_WithASourcePage_CarriesItIntoTheTally()
	{
		Running(Documentaries);
		Reads(Batch([], new None()) with { Progress = new RunProgress(Some.Of(new SourcePage(3, new None())), 2) });
		using var testee = Progress();

		await testee.StartAsync();

		await EventuallyAsync(() => testee.Live.Match(run => run.Tally.Progress.Waiting == 2, () => false));
		var live = (await Assert.That(testee.Live).IsSome())!;
		await Assert.That(live.Tally.Progress.Page).IsSome().And.IsEqualTo(new SourcePage(3, new None()));
	}

	[Test]
	public async Task Batch_WithTitlesHeldAtARateLimitedService_MarksTheTallyRateLimited()
	{
		Running(Documentaries);
		var progress = new RunProgress(Some.Of(new SourcePage(1, new None())), 3) { Busiest = Some.Of(new ServiceWait(RemoteService.Tmdb, 3)) };
		Reads(Batch([], new None()) with { Progress = progress });
		_capacity.Of(RemoteService.Tmdb).Returns(new RemoteLoad(Some.Of(Start.AddSeconds(9)), 0));
		using var testee = Progress();

		await testee.StartAsync();

		await EventuallyAsync(() => testee.Live.Match(run => run.Tally.Progress.Waiting == 3, () => false));
		await Assert.That((await Assert.That(testee.Live).IsSome())!.Tally.RateLimited).IsTrue();
	}

	[Test]
	public async Task Batch_WhileTheListReadsAPage_SendsThePageToTheView()
	{
		Running(Documentaries);
		Reads(Batch([], new None()) with { Progress = RunProgress.None with { Reading = Some.Of(2) } });
		var batches = new List<WireBatch>();
		using var testee = Progress();
		await testee.StartAsync();
		await EventuallyAsync(() => testee.Live.IsSome());

		(await Assert.That(testee.Live).IsSome())!.Batch += batches.Add;

		await EventuallyAsync(() => batches.Count > 0);
		await Assert.That(batches[0].Reading).IsEqualTo(2);
		await Assert.That((await Assert.That(testee.Live).IsSome())!.Tally.Progress.Reading).IsSome().And.IsEqualTo(2);
	}

	private void Reads(TraceRead read) => _traces.ReadAsync(Any<FeedId>(), Any<TraceCursor>(), Any()).Returns(read);

	private void Running(Feed feed)
	{
		var job = new PendingJob(JobId.From(Guid.NewGuid()), new RunFeed(feed.Id, new Manual()), JobState.Running, Start, 1);
		_queue.GetAsync(Any()).Returns(new QueueSnapshot([job], 0));
	}

	private FeedRunProgress Progress()
	{
		return new FeedRunProgress(_queue.Object, _events.Object, _feeds.Object, _layouts.Object, _traces.Object, _capacity.Object, _user.Object, _clock.Object);
	}

	private static void Hold(ManualResetEventSlim entered, ManualResetEventSlim release)
	{
		entered.Set();
		release.Wait(TimeSpan.FromSeconds(5));
	}

	private static JobStateChangedEvent Job(Feed feed, JobState state) => new(Owner, JobKind.FeedRun, feed.Id.Value.ToString(), state);

	private static FeedOverview Overview(Feed feed) => new(feed, Some.Of(Previous(feed, 1999)), FeedRunHistory.None, 0);

	private static TraceSnapshot Snapshot(FateTally fates, Optional<int> requests)
	{
		return new TraceSnapshot(new TraceCursor(Some.Of(Run), 5), 1200, [], [], fates, requests, new None());
	}

	private static TraceBatch Batch(IReadOnlyList<TraceStep> steps, Optional<FeedRunEnd> ended)
	{
		return new TraceBatch(new TraceCursor(Some.Of(Run), 9), 2000, steps, FateTally.None, new None(), ended);
	}

	private static FeedRun Previous(Feed feed, int total)
	{
		return new FeedRun
		{
			Id = RunId.From(Guid.NewGuid()),
			FeedId = feed.Id,
			StartedAt = Start.AddHours(-1),
			Duration = TimeSpan.FromSeconds(19),
			Strategy = new Append(),
			Trigger = new Manual(),
			Conclusiveness = Conclusiveness.Conclusive,
			Counts = FeedRunCounts.None with { Total = total },
			Items = [],
		};
	}

	private static async Task EventuallyAsync(Func<bool> condition)
	{
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		while (!condition())
		{
			await Task.Delay(20, timeout.Token);
		}
	}
}
