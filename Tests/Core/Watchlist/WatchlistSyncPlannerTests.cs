using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Domain.Media;
using Domain.Watchlist.Outbox;

namespace Core.Tests.Watchlist;

public class WatchlistSyncPlannerTests
{
	[Test]
	public async Task PlanAsync_WhenAQueuedRemovalTheProviderAlreadyAgreesWith_DropsItWithoutRunningASync()
	{
		var stale = MediaId.From(new Guid(11, 0, 0, new byte[8]));
		var outbox = IWatchlistOutbox.Mock();
		outbox.GetAsync(Any()).Returns([new WatchlistOutboxEntry(stale, WatchlistOperationAction.Remove, WatchlistOutboxState.Pending, 0)]);
		var provider = IWatchlistProvider.Mock();
		provider.GetWatchlistAsync(Any()).Returns(WatchlistStubs.NothingRemote());

		await WatchlistStubs.Planner(provider, outbox).PlanAsync();

		outbox.DropAsync(Any<IEnumerable<MediaId>>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task PlanAsync_WhenTheOutboxAgreesWithTheProvider_LeavesItAlone()
	{
		var provider = IWatchlistProvider.Mock();
		provider.GetWatchlistAsync(Any()).Returns(WatchlistStubs.NothingRemote());
		var outbox = WatchlistStubs.EmptyOutbox();

		await WatchlistStubs.Planner(provider, outbox).PlanAsync();

		outbox.DropAsync(Any<IEnumerable<MediaId>>(), Any()).WasNeverCalled();
	}
}
