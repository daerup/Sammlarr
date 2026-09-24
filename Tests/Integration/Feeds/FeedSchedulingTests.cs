using Contracts.Feed;
using Domain.Feed;
using Domain.Identity;
using Domain.Providers;
using Harness;

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
	public async Task CreateFeedAsync_SchedulesTheFeedItJustSavedOnItsOwnCron()
	{
		var id = await Testee.CreateFeedAsync(Feed("Nightly", new Scheduled(Nightly)));

		await Assert.That(Scheduler.Scheduled).Contains((id, new Scheduled(Nightly)));
	}

	[Test]
	public async Task CreateFeedAsync_WhenTheFeedIsManual_LeavesItUnscheduled()
	{
		var id = await Testee.CreateFeedAsync(Feed("Hand cranked", new Manual()));

		await Assert.That(Scheduler.Scheduled.Select(scheduled => scheduled.Id)).DoesNotContain(id);
	}

	[Test]
	public async Task UpdateFeedAsync_WhenTheFeedBecomesScheduled_SchedulesItOnTheNewCron()
	{
		var id = await Testee.CreateFeedAsync(Feed("Hand cranked", new Manual()));

		await Testee.UpdateFeedAsync(id, Update("Hand cranked", new Scheduled(Hourly), isEnabled: true));

		await Assert.That(Scheduler.Scheduled).Contains((id, new Scheduled(Hourly)));
	}

	[Test]
	public async Task UpdateFeedAsync_WhenTheFeedIsReEnabled_SchedulesItAgain()
	{
		var id = await Testee.CreateFeedAsync(Feed("Nightly", new Scheduled(Nightly)));
		await Testee.UpdateFeedAsync(id, Update("Nightly", new Scheduled(Nightly), isEnabled: false));

		await Testee.UpdateFeedAsync(id, Update("Nightly", new Scheduled(Nightly), isEnabled: true));

		await Assert.That(Scheduler.Scheduled.Count(scheduled => scheduled.Id == id)).IsEqualTo(2);
	}

	[Test]
	public async Task UpdateFeedAsync_WhenTheFeedBecomesManual_UnschedulesIt()
	{
		var id = await Testee.CreateFeedAsync(Feed("Nightly", new Scheduled(Nightly)));

		await Testee.UpdateFeedAsync(id, Update("Nightly", new Manual(), isEnabled: true));

		await Assert.That(Scheduler.Unscheduled).Contains(id);
	}

	[Test]
	public async Task UpdateFeedAsync_WhenTheFeedIsDisabled_UnschedulesItWhateverItsCronSays()
	{
		var id = await Testee.CreateFeedAsync(Feed("Nightly", new Scheduled(Nightly)));

		await Testee.UpdateFeedAsync(id, Update("Nightly", new Scheduled(Nightly), isEnabled: false));

		await Assert.That(Scheduler.Unscheduled).Contains(id);
	}

	[Test]
	public async Task DeleteFeedAsync_UnschedulesBeforeItRemovesTheFeed()
	{
		var id = await Testee.CreateFeedAsync(Feed("Doomed", new Scheduled(Nightly)));

		await Testee.DeleteFeedAsync(id);

		await Assert.That(Scheduler.Unscheduled).Contains(id);
		await Assert.That(await Testee.GetFeedByIdAsync(id)).IsNull();
	}

	[Test]
	public async Task DeleteFeedAsync_WhenTheFeedBelongsToAnotherUser_LeavesTheirScheduleAndFeedIntact()
	{
		var id = await Testee.CreateFeedAsync(Feed("Theirs", new Scheduled(Nightly)));
		var owner = host.CurrentUser.Id;
		host.CurrentUser.Id = UserId.From(SammlarrTestHost.SeededMember.Id);

		await Testee.DeleteFeedAsync(id);

		host.CurrentUser.Id = owner;
		await Assert.That(Scheduler.Unscheduled).DoesNotContain(id);
		await Assert.That(await Testee.GetFeedByIdAsync(id)).IsNotNull();
	}

	private static FeedCreateData Feed(string name, FeedRunTrigger schedule) =>
		new()
		{
			Name = name,
			IsEnabled = true,
			Provider = FeedProvider.Mdblist,
			Strategy = new FeedStrategy(new Append()),
			Identifier = name.ToLowerInvariant(),
			Schedule = schedule,
		};

	private static FeedUpdateData Update(string name, FeedRunTrigger schedule, bool isEnabled) =>
		new()
		{
			Name = name,
			IsEnabled = isEnabled,
			Provider = FeedProvider.Mdblist,
			Strategy = new FeedStrategy(new Append()),
			Identifier = name.ToLowerInvariant(),
			Schedule = schedule,
		};
}
