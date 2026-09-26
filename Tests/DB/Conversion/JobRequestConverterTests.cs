using DB.Conversion.Jobs;
using Domain.Feed;
using Domain.Jobs;
using Domain.Watchlist.Sync;
using TestInfrastructure;

namespace DB.Tests.Conversion;

public class JobRequestConverterTests
{
	private readonly JobRequestConverter _testee = new();

	[Test]
	public async Task ConvertToProvider_WhenAScheduledFeedRun_WritesTheFeedAndItsCron()
	{
		var id = FeedId.From(Guid.NewGuid());

		var stored = _testee.ConvertToProvider(new RunFeed(id, new Scheduled("0 0 3 * * *")));

		await Assert.That(stored).IsEqualTo($"feed:{id.Value}:0 0 3 * * *");
	}

	[Test]
	public async Task Convert_WhenAManualFeedRun_RoundTripsTheTrigger()
	{
		var original = new RunFeed(FeedId.From(Guid.NewGuid()), new Manual());

		var restored = (RunFeed)_testee.ConvertFromProvider(_testee.ConvertToProvider(original))!;

		await Assert.That(restored.Id).IsEqualTo(original.Id);
		await Assert.That(restored.Trigger).IsOfUnionType<FeedRunTrigger, Manual>();
	}

	[Test]
	public async Task Convert_WhenAManualSync_RoundTripsTheTrigger()
	{
		var restored = (SyncWatchlist)_testee.ConvertFromProvider(_testee.ConvertToProvider(new SyncWatchlist(new ManualTrigger())))!;

		await Assert.That(restored.Trigger).IsOfUnionType<WatchlistSyncTrigger, ManualTrigger>();
	}

	[Test]
	public async Task Convert_WhenAScheduledSync_RoundTripsTheTriggerRatherThanFallingBackToManual()
	{
		var restored = (SyncWatchlist)_testee.ConvertFromProvider(_testee.ConvertToProvider(new SyncWatchlist(new ScheduledTrigger())))!;

		await Assert.That(restored.Trigger).IsOfUnionType<WatchlistSyncTrigger, ScheduledTrigger>();
	}
}
