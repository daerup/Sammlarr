using Contracts.Feed;
using Domain.Feed;
using Domain.Providers;
using Harness;

namespace Integration.Tests.Feeds;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class FeedSchedulingTests(SammlarrTestHost host) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	private IFeedService Testee => host.Resolve<IFeedService>();

	private RecordingFeedScheduler Scheduler => host.Resolve<RecordingFeedScheduler>();

	[Test]
	public async Task CreateFeedAsync_SchedulesTheFeedItJustSaved()
	{
		var id = await Testee.CreateFeedAsync(Feed("Nightly", new Scheduled("0 0 3 * * *")));

		await Assert.That(Scheduler.Scheduled.Select(scheduled => scheduled.Id)).Contains(id);
	}

	[Test]
	public async Task CreateFeedAsync_WhenTheFeedIsManual_LeavesItUnscheduled()
	{
		var id = await Testee.CreateFeedAsync(Feed("Hand cranked", new Manual()));

		await Assert.That(Scheduler.Scheduled.Select(scheduled => scheduled.Id)).DoesNotContain(id);
	}

	[Test]
	public async Task UpdateFeedAsync_WhenTheFeedBecomesManual_UnschedulesIt()
	{
		var id = await Testee.CreateFeedAsync(Feed("Nightly", new Scheduled("0 0 3 * * *")));

		await Testee.UpdateFeedAsync(id, Update("Nightly", new Manual(), isEnabled: true));

		await Assert.That(Scheduler.Unscheduled).Contains(id);
	}

	[Test]
	public async Task UpdateFeedAsync_WhenTheFeedIsDisabled_UnschedulesItWhateverItsCronSays()
	{
		var id = await Testee.CreateFeedAsync(Feed("Nightly", new Scheduled("0 0 3 * * *")));

		await Testee.UpdateFeedAsync(id, Update("Nightly", new Scheduled("0 0 3 * * *"), isEnabled: false));

		await Assert.That(Scheduler.Unscheduled).Contains(id);
	}

	[Test]
	public async Task DeleteFeedAsync_UnschedulesBeforeItRemovesTheFeed()
	{
		var id = await Testee.CreateFeedAsync(Feed("Doomed", new Scheduled("0 0 3 * * *")));

		await Testee.DeleteFeedAsync(id);

		await Assert.That(Scheduler.Unscheduled).Contains(id);
		await Assert.That(await Testee.GetFeedByIdAsync(id)).IsNull();
	}

	[Test]
	public async Task DeleteFeedAsync_WhenTheFeedIsNotVisible_LeavesItsScheduleAlone()
	{
		var someoneElses = FeedId.From(Guid.NewGuid());

		await Testee.DeleteFeedAsync(someoneElses);

		await Assert.That(Scheduler.Unscheduled).DoesNotContain(someoneElses);
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
