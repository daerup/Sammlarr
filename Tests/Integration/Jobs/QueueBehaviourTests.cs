using Contracts;
using Contracts.Feed;
using Contracts.Jobs;
using Core.Jobs;
using Domain.Feed;
using Domain.Jobs;
using Domain.Providers;
using Domain.Wantlist.Sync;
using Harness;
using Integration.Tests.Feeds;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Options;
using TestInfrastructure;

namespace Integration.Tests.Jobs;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
[NotInParallel]
public class QueueBehaviourTests(SammlarrTestHost host) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	private IJobQueue Testee => host.Resolve<IJobQueue>();

	private static readonly DateTimeOffset Three = new(2024, 6, 1, 3, 0, 0, TimeSpan.Zero);
	private static readonly SyncWantlist ManualSync = new(new ManualTrigger());

	private FeedId _documentaries;
	private FeedId _anime;

	private RunFeed DocumentaryFeed => new(_documentaries, new Manual());

	private RunFeed AnimeFeed => new(_anime, new Manual());

	private SchedulerOptions Options => host.Resolve<SchedulerOptions>();

	[Before(Test)]
	public async Task TwoNightlyFeeds()
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		await host.RemoteWantlistEmpty();
		_documentaries = await FeedAsync("Documentaries", "0 0 3 * * *");
		_anime = await FeedAsync("Anime", "0 1 3 * * *");
		foreach (var fill in (await host.Resolve<IJobQueueView>().GetAsync()).Jobs.Where(job => job.Request is FillDetails))
		{
			await Testee.CancelAsync(fill.Id);
		}
	}

	[Test]
	public async Task AScheduledFeedRun_ArmsTheSyncAsItStarts()
	{
		await ClockReachesAsync(Three);
		await CronFiresAsync(_documentaries);

		var duringTheRun = await WhileTheNextJobRunsAsync(host.HoldMdblistFeed(FeedRunSeeding.ListOf("Documentaries")), ArmedSyncAsync);

		await Assert.That(duringTheRun.State).IsEqualTo(JobState.Waiting);
	}

	[Test]
	public async Task AScheduledFeedRun_ArmsASyncThatWaitsOutTheGrace()
	{
		await ClockReachesAsync(Three);
		var documentaries = await CronFiresAsync(_documentaries);

		await RunNextJobAsync();

		var ran = await JobAsync(documentaries.Id);
		var sync = await ArmedSyncAsync();
		await Assert.That(ran.State).IsEqualTo(JobState.Done);
		await Assert.That(sync.State).IsEqualTo(JobState.Waiting);
		await Assert.That(sync.RunAfter).IsEqualTo(Three + Options.Grace);
	}

	[Test]
	public async Task RunningAFeedWithSyncAfter_ArmsTheSyncOnlyOnceTheRunStarts()
	{
		await EnqueueAsync(DocumentaryFeed with { SyncAfter = true });
		var beforeTheRun = await QueueAsync();

		await RunNextJobAsync();

		var sync = await ArmedSyncAsync();
		await Assert.That(beforeTheRun).Count().IsEqualTo(1);
		await Assert.That(sync.State).IsEqualTo(JobState.Waiting);
	}

	[Test]
	public async Task AFeedQueuedWhileTheSyncWaits_QueuesTheSyncBehindIt()
	{
		var armed = await SyncArmedByDocumentariesAsync();
		await ClockReachesAsync(Three.AddMinutes(1));

		await CronFiresAsync(_anime);

		var sync = await ArmedSyncAsync();
		await Assert.That(sync.Id).IsEqualTo(armed.Id);
		await Assert.That(sync.State).IsEqualTo(JobState.Queued);
	}

	[Test]
	public async Task AnotherFeedRunningInsideTheGrace_GoesFirstAndPushesTheSyncBack()
	{
		var armed = await SyncArmedByDocumentariesAsync();
		await ClockReachesAsync(Three.AddMinutes(1));
		await CronFiresAsync(_anime);

		await RunNextJobAsync();

		var sync = await ArmedSyncAsync();
		await Assert.That(sync.Id).IsEqualTo(armed.Id);
		await Assert.That(sync.State).IsEqualTo(JobState.Waiting);
		await Assert.That(sync.RunAfter).IsEqualTo(Three.AddMinutes(1) + Options.Grace);
	}

	[Test]
	public async Task APlainManualRun_LeavesTheWaitingSyncToItsGrace()
	{
		var armed = await SyncArmedByDocumentariesAsync();
		await ClockReachesAsync(Three.AddSeconds(30));
		await EnqueueAsync(AnimeFeed);
		var whileQueued = await ArmedSyncAsync();

		await RunNextJobAsync();

		var sync = await ArmedSyncAsync();
		await Assert.That(whileQueued.State).IsEqualTo(JobState.Waiting);
		await Assert.That(sync.State).IsEqualTo(JobState.Waiting);
		await Assert.That(sync.RunAfter).IsEqualTo(armed.RunAfter);
	}

	[Test]
	public async Task AFeedThatKeepsFinishing_CannotHoldTheSyncBackPastMaxWait()
	{
		var trending = await FeedAsync("Trending", "0 * * * * *");
		await ClockReachesAsync(Three);
		await CronFiresAsync(trending);
		await RunQueueAsync();
		var armed = await ArmedSyncAsync();
		var ceiling = Three + Options.Grace + Options.MaxWait;

		for (var minute = Three.AddMinutes(1); minute <= ceiling; minute = minute.AddMinutes(1))
		{
			await ClockReachesAsync(minute);
			await CronFiresAsync(trending);
			await RunQueueAsync();
		}

		var sync = await JobAsync(armed.Id);
		await Assert.That(sync.State).IsEqualTo(JobState.Done);
		await Assert.That(sync.RunAfter).IsEqualTo(ceiling);
	}

	[Test]
	public async Task TheArmedSync_RunsOnceTheGraceHasPassed()
	{
		var armed = await SyncArmedByDocumentariesAsync();

		await ClockReachesAsync(armed.RunAfter);
		await RunNextJobAsync();

		var sync = await JobAsync(armed.Id);
		await Assert.That(sync.State).IsEqualTo(JobState.Done);
	}

	[Test]
	public async Task PressingSyncWhileOneIsWaiting_PullsItForwardToRunNowInsteadOfAddingAnother()
	{
		var armed = await SyncArmedByDocumentariesAsync();

		var pressed = await EnqueueAsync(ManualSync);

		await Assert.That(pressed.Id).IsEqualTo(armed.Id);
		await Assert.That(pressed.State).IsEqualTo(JobState.Queued);
	}

	[Test]
	public async Task PressingSync_RunsRightAfterTheRunsQueuedBeforeItWithoutAGrace()
	{
		var pressedAt = Three.AddSeconds(90);
		await ClockReachesAsync(Three);
		await CronFiresAsync(_documentaries);
		var (anime, pressed) = await WhileTheNextJobRunsAsync(
			host.HoldMdblistFeed(FeedRunSeeding.ListOf("Documentaries")),
			async () =>
			{
				await ClockReachesAsync(Three.AddMinutes(1));
				var queuedBeforeThePress = await CronFiresAsync(_anime);
				await ClockReachesAsync(pressedAt);
				return (queuedBeforeThePress, await EnqueueAsync(ManualSync));
			}
		);

		await RunNextJobAsync();

		var ranFirst = await JobAsync(anime.Id);
		var sync = await JobAsync(pressed.Id);
		await Assert.That(ranFirst.State).IsEqualTo(JobState.Done);
		await Assert.That(sync.State).IsEqualTo(JobState.Queued);
		await Assert.That(sync.RunAfter).IsEqualTo(pressedAt);
	}

	[Test]
	public async Task PressingSync_KeepsItsPlaceAheadOfRunsQueuedAfterIt()
	{
		await ClockReachesAsync(Three);
		await CronFiresAsync(_documentaries);
		var (pressed, anime) = await WhileTheNextJobRunsAsync(
			host.HoldMdblistFeed(FeedRunSeeding.ListOf("Documentaries")),
			async () =>
			{
				var pressedSync = await EnqueueAsync(ManualSync);
				await ClockReachesAsync(Three.AddMinutes(1));
				return (pressedSync, await CronFiresAsync(_anime));
			}
		);

		await RunNextJobAsync();

		var sync = await JobAsync(pressed.Id);
		var waitsItsTurn = await JobAsync(anime.Id);
		await Assert.That(sync.State).IsEqualTo(JobState.Done);
		await Assert.That(waitsItsTurn.State).IsEqualTo(JobState.Queued);
	}

	[Test]
	public async Task PressingSyncWhileOneIsRunning_QueuesAFollowUpSoTitlesAddedMeanwhileAreNotLost()
	{
		var running = await EnqueueAsync(ManualSync);

		var followUp = await WhileTheNextJobRunsAsync(host.HoldRemoteWantlist(), () => EnqueueAsync(ManualSync));

		await Assert.That(followUp.Id).IsNotEqualTo(running.Id);
		await Assert.That(followUp.State).IsEqualTo(JobState.Queued);
	}

	[Test]
	public async Task RunningTwoDifferentFeeds_QueuesBoth()
	{
		var (documentaryJob, animeJob) = await EnqueueAsync([DocumentaryFeed, AnimeFeed]);

		await Assert.That(animeJob.Id).IsNotEqualTo(documentaryJob.Id);
		await Assert.That(documentaryJob.State).IsEqualTo(JobState.Queued);
		await Assert.That(animeJob.State).IsEqualTo(JobState.Queued);
	}

	[Test]
	public async Task RunningTheSameFeedTwice_KeepsTheOneAlreadyQueued()
	{
		var (first, second) = await EnqueueAsync([DocumentaryFeed, DocumentaryFeed]);

		await Assert.That(second.Id).IsEqualTo(first.Id);
		await Assert.That(second.State).IsEqualTo(JobState.Queued);
	}

	[Test]
	public async Task RunningAFeedThatIsAlreadyRunning_DoesNotQueueItAgain()
	{
		var running = await EnqueueAsync(DocumentaryFeed);

		var again = await WhileTheNextJobRunsAsync(host.HoldMdblistFeed(FeedRunSeeding.ListOf("Documentaries")), () => EnqueueAsync(DocumentaryFeed));

		var queue = await QueueAsync();
		await Assert.That(again.Id).IsEqualTo(running.Id);
		await Assert.That(again.State).IsEqualTo(JobState.Running);
		await Assert.That(queue).IsEmpty();
	}

	[Test]
	public async Task CancellingAQueuedRun_TakesItOutOfTheQueueWithoutRunningIt()
	{
		var queued = await EnqueueAsync(DocumentaryFeed);

		var cancelled = await Testee.CancelAsync(queued.Id);

		await Assert.That(cancelled).IsSome().And.IsEqualTo(DocumentaryFeed);
		await Assert.That((await JobAsync(queued.Id)).State).IsEqualTo(JobState.Cancelled);
		await Assert.That(await RunNextJobAsync()).IsFalse();
	}

	[Test]
	public async Task RunningAFeedAgainAfterCancellingIt_QueuesAFreshRun()
	{
		var cancelled = await EnqueueAsync(DocumentaryFeed);
		await Testee.CancelAsync(cancelled.Id);

		var again = await EnqueueAsync(DocumentaryFeed);

		await Assert.That(again.Id).IsNotEqualTo(cancelled.Id);
		await Assert.That(again.State).IsEqualTo(JobState.Queued);
	}

	[Test]
	public async Task CancellingTheSyncWhileItWaitsOutItsGrace_TakesItOutOfTheQueue()
	{
		var sync = await SyncArmedByDocumentariesAsync();

		await Testee.CancelAsync(sync.Id);

		await Assert.That((await JobAsync(sync.Id)).State).IsEqualTo(JobState.Cancelled);
		await Assert.That(await QueueAsync()).IsEmpty();
	}

	[Test]
	public async Task CancellingARunThatAlreadyStarted_LetsItFinish()
	{
		var running = await EnqueueAsync(DocumentaryFeed);

		var cancelled = await WhileTheNextJobRunsAsync(host.HoldMdblistFeed(FeedRunSeeding.ListOf("Documentaries")), () => Testee.CancelAsync(running.Id));

		await Assert.That(cancelled).IsNone();
		await Assert.That((await JobAsync(running.Id)).State).IsEqualTo(JobState.Done);
	}

	private async Task<FeedId> FeedAsync(string name, string cron)
	{
		var identifier = FeedRunSeeding.ListOf(name);
		host.MdblistFeedEmpty(identifier);
		return await host.Resolve<IFeedService>()
			.CreatedAsync(
				new FeedCreateData
				{
					Name = name,
					IsEnabled = true,
					Provider = FeedProvider.Mdblist,
					Strategy = new FeedStrategy(new Append()),
					Identifier = identifier,
					Schedule = new Scheduled(cron),
				}
			);
	}

	private async Task<PendingJob> SyncArmedByDocumentariesAsync()
	{
		await ClockReachesAsync(Three);
		await CronFiresAsync(_documentaries);
		await RunNextJobAsync();
		return await ArmedSyncAsync();
	}

	private async Task<PendingJob> CronFiresAsync(FeedId feed)
	{
		var scheduled = (await Assert.That(await host.Resolve<IFeedRepository>().FindAsync(feed)).IsSome())!;
		return await EnqueueAsync(new RunFeed(feed, scheduled.Schedule));
	}

	private async Task ClockReachesAsync(DateTimeOffset at)
	{
		host.SetCurrentTime(at);
		await host.Resolve<IJobRepository>().ReleaseDueAsync(at);
	}

	private async Task<bool> RunNextJobAsync() =>
		await new JobRunner(
			host.Resolve<IServiceScopeFactory>(),
			host.Resolve<JobSignal>(),
			host.Resolve<JobAlarm>(),
			host.Resolve<JobRunnerStatus>(),
			host.Resolve<IDateTimeProvider>(),
			NullLogger<JobRunner>.Instance
		).TryRunNextAsync(CancellationToken.None);

	private async Task RunQueueAsync()
	{
		while (await RunNextJobAsync()) { }
	}

	private async Task<T> WhileTheNextJobRunsAsync<T>(HeldRequest held, Func<Task<T>> meanwhile)
	{
		var running = RunNextJobAsync();
		await held.Arrived;
		var result = await meanwhile();
		held.Release();
		await running;
		return result;
	}

	private async Task<PendingJob> ArmedSyncAsync() => (await QueueAsync()).Single(job => job.Request is SyncWantlist);

	private async Task<PendingJob> JobAsync(JobId id)
	{
		var raw = id.Value;
		await using var context = await host.ContextFactory.CreateDbContextAsync();
		var job = await context.Jobs.AsNoTracking().SingleAsync(row => row.Id == raw);
		return new PendingJob(id, job.Request, job.State, job.RunAfter, job.Attempts);
	}

	private async Task<PendingJob> EnqueueAsync(IJobRequest request) => (await EnqueueAsync([request]))[0];

	private async Task<IReadOnlyList<PendingJob>> EnqueueAsync(IReadOnlyList<IJobRequest> requests)
	{
		List<JobId> ids = [];
		foreach (var request in requests)
		{
			ids.Add(IdOf(await Testee.EnqueueAsync(request)));
		}

		var queue = await QueueAsync();
		return [.. ids.Select(id => queue.Single(job => job.Id == id))];
	}

	private static JobId IdOf(EnqueueResult enqueued) =>
		enqueued switch
		{
			Queued queued => queued.Id,
			AlreadyQueued kept => kept.Id,
		};

	private async Task<IReadOnlyList<PendingJob>> QueueAsync()
	{
		return [.. (await host.Resolve<IJobQueueView>().GetAsync()).Jobs.Where(job => job.Request is not FillDetails)];
	}
}

file static class PendingJobDeconstruction
{
	extension(IReadOnlyList<PendingJob> jobs)
	{
		public void Deconstruct(out PendingJob first, out PendingJob second)
		{
			first = jobs[0];
			second = jobs[1];
		}
	}
}
