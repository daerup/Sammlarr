using Contracts.Feed;
using Contracts.Wantlist;
using Domain.Feed;
using Domain.Identity;
using Domain.Providers;
using Domain.Wantlist;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Feeds;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class FeedSchedulingTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string Nightly = "0 0 3 * * *";
	private const string Hourly = "0 0 * * * *";

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	private IFeedService Testee => host.Resolve<IFeedService>();

	private RecordingFeedScheduler Scheduler => host.Resolve<RecordingFeedScheduler>();

	[Test]
	public async Task CreateAsync_SchedulesTheFeedItJustSavedOnItsOwnCron()
	{
		var id = await Testee.CreatedAsync(Feed("Nightly", new Scheduled(Nightly)));

		await Assert.That(Scheduler.Scheduled).Contains((id, new Scheduled(Nightly)));
	}

	[Test]
	public async Task CreateAsync_WhenTheFeedIsManual_LeavesItUnscheduled()
	{
		var id = await Testee.CreatedAsync(Feed("Hand cranked", new Manual()));

		await Assert.That(Scheduler.Scheduled.Select(scheduled => scheduled.Id)).DoesNotContain(id);
	}

	[Test]
	public async Task UpdateAsync_WhenTheFeedBecomesScheduled_SchedulesItOnTheNewCron()
	{
		var id = await Testee.CreatedAsync(Feed("Hand cranked", new Manual()));

		await Testee.UpdateAsync(id, Update("Hand cranked", new Scheduled(Hourly), isEnabled: true));

		await Assert.That(Scheduler.Scheduled).Contains((id, new Scheduled(Hourly)));
	}

	[Test]
	public async Task UpdateAsync_WhenTheFeedIsReEnabled_SchedulesItAgain()
	{
		var id = await Testee.CreatedAsync(Feed("Nightly", new Scheduled(Nightly)));
		await Testee.UpdateAsync(id, Update("Nightly", new Scheduled(Nightly), isEnabled: false));

		await Testee.UpdateAsync(id, Update("Nightly", new Scheduled(Nightly), isEnabled: true));

		await Assert.That(Scheduler.Scheduled.Count(scheduled => scheduled.Id == id)).IsEqualTo(2);
	}

	[Test]
	public async Task UpdateAsync_WhenTheFeedBecomesManual_UnschedulesIt()
	{
		var id = await Testee.CreatedAsync(Feed("Nightly", new Scheduled(Nightly)));

		await Testee.UpdateAsync(id, Update("Nightly", new Manual(), isEnabled: true));

		await Assert.That(Scheduler.Unscheduled).Contains(id);
	}

	[Test]
	public async Task UpdateAsync_WhenTheFeedIsDisabled_UnschedulesItWhateverItsCronSays()
	{
		var id = await Testee.CreatedAsync(Feed("Nightly", new Scheduled(Nightly)));

		await Testee.UpdateAsync(id, Update("Nightly", new Scheduled(Nightly), isEnabled: false));

		await Assert.That(Scheduler.Unscheduled).Contains(id);
	}

	[Test]
	public async Task DeleteAsync_UnschedulesBeforeItRemovesTheFeed()
	{
		var id = await Testee.CreatedAsync(Feed("Doomed", new Scheduled(Nightly)));

		await Testee.DeleteAsync(id);

		await Assert.That(Scheduler.Unscheduled).Contains(id);
		await Assert.That((await Testee.GetAsync()).Find(id)).IsNone();
	}

	[Test]
	public async Task DeleteAsync_ForAFeedWithTitlesOnTheWantlist_KeepsThemAsOrphanedUnderItsName()
	{
		var id = await Testee.CreatedAsync(Feed("Nightly", new Scheduled(Nightly)));
		var title = await host.Movie(TestMedia.Inception).OnWantlist(await host.SeedFeedOriginAsync(id));

		await Testee.DeleteAsync(id);

		var item = await Assert.That((await host.Resolve<IWantlistService>().GetAsync()).Find(title)).IsSome();
		await Assert
			.That(item!.Provenance)
			.IsOfUnionType<WantlistItemProvenance, OrphanedOrigin>()
			.And.Multiple(async orphan => await Assert.That(orphan.FormerFeedName).IsEqualTo("Nightly"));
	}

	[Test]
	public async Task DeleteAsync_WhenTheFeedBelongsToAnotherUser_LeavesTheirScheduleAndFeedIntact()
	{
		var id = await Testee.CreatedAsync(Feed("Theirs", new Scheduled(Nightly)));
		var owner = host.CurrentUser.Id;
		host.CurrentUser.Id = UserId.From(SammlarrTestHost.SeededMember.Id);

		await Testee.DeleteAsync(id);

		host.CurrentUser.Id = owner;
		await Assert.That(Scheduler.Unscheduled).DoesNotContain(id);
		await Assert.That((await Testee.GetAsync()).Find(id)).IsSome();
	}

	[Test]
	public async Task PauseAsync_ForTheSelection_DisablesAndUnschedulesEverySelectedFeed()
	{
		var nightly = await Testee.CreatedAsync(Feed("Nightly", new Scheduled(Nightly)));
		var hourly = await Testee.CreatedAsync(Feed("Hourly", new Scheduled(Hourly)));

		await Testee.PauseAsync([nightly, hourly]);

		await Assert.That((await host.FeedAsync(nightly)).Enabled).IsFalse();
		await Assert.That((await host.FeedAsync(hourly)).Enabled).IsFalse();
		await Assert.That(Scheduler.Unscheduled).Contains(nightly).And.Contains(hourly);
	}

	[Test]
	public async Task ResumeAsync_AfterAPause_SchedulesTheFeedOnItsOwnCronAgain()
	{
		var id = await Testee.CreatedAsync(Feed("Nightly", new Scheduled(Nightly)));
		await Testee.PauseAsync([id]);

		await Testee.ResumeAsync([id]);

		await Assert.That((await host.FeedAsync(id)).Enabled).IsTrue();
		await Assert.That(Scheduler.Scheduled.Count(scheduled => scheduled == (id, new Scheduled(Nightly)))).IsEqualTo(2);
	}

	[Test]
	public async Task PauseAsync_ForFeedsOutsideTheSelection_LeavesThemRunning()
	{
		var paused = await Testee.CreatedAsync(Feed("Paused", new Scheduled(Nightly)));
		var untouched = await Testee.CreatedAsync(Feed("Untouched", new Scheduled(Nightly)));

		await Testee.PauseAsync([paused]);

		await Assert.That((await host.FeedAsync(untouched)).Enabled).IsTrue();
		await Assert.That(Scheduler.Unscheduled).DoesNotContain(untouched);
	}

	[Test]
	public async Task PauseAsync_WhenAFeedIsAlreadyPaused_ReturnsOnlyTheFeedsItChanged()
	{
		var enabled = await Testee.CreatedAsync(Feed("Enabled", new Scheduled(Nightly)));
		var alreadyPaused = await Testee.CreatedAsync(Feed("Already paused", new Scheduled(Nightly)));
		await Testee.PauseAsync([alreadyPaused]);

		var changed = await Testee.PauseAsync([enabled, alreadyPaused]);

		await Assert.That(changed).IsEquivalentTo([enabled]);
	}

	[Test]
	public async Task ResumeAsync_WhenAFeedIsAlreadyRunning_ReturnsOnlyTheFeedsItChanged()
	{
		var paused = await Testee.CreatedAsync(Feed("Paused", new Scheduled(Nightly)));
		var running = await Testee.CreatedAsync(Feed("Running", new Scheduled(Nightly)));
		await Testee.PauseAsync([paused]);

		var changed = await Testee.ResumeAsync([paused, running]);

		await Assert.That(changed).IsEquivalentTo([paused]);
	}

	[Test]
	public async Task PauseAsync_WhenTheFeedBelongsToAnotherUser_LeavesItRunning()
	{
		var id = await Testee.CreatedAsync(Feed("Theirs", new Scheduled(Nightly)));
		var owner = host.CurrentUser.Id;
		host.CurrentUser.Id = UserId.From(SammlarrTestHost.SeededMember.Id);

		await Testee.PauseAsync([id]);

		host.CurrentUser.Id = owner;
		await Assert.That((await host.FeedAsync(id)).Enabled).IsTrue();
		await Assert.That(Scheduler.Unscheduled).DoesNotContain(id);
	}

	private static FeedCreateData Feed(string name, FeedRunTrigger schedule) =>
		new()
		{
			Name = name,
			IsEnabled = true,
			Provider = FeedProvider.Mdblist,
			Strategy = new FeedStrategy(new Append()),
			Identifier = FeedRunSeeding.ListOf(name),
			Schedule = schedule,
		};

	private static FeedUpdateData Update(string name, FeedRunTrigger schedule, bool isEnabled) =>
		new()
		{
			Name = name,
			IsEnabled = isEnabled,
			Provider = FeedProvider.Mdblist,
			Strategy = new FeedStrategy(new Append()),
			Identifier = FeedRunSeeding.ListOf(name),
			Schedule = schedule,
		};
}
