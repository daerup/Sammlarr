using Contracts.Wantlist;
using Contracts.Wantlist.Outbox;
using Domain.Media;
using Domain.Wantlist.Outbox;

namespace Core.Tests.Wantlist;

public class WantlistSyncPlannerTests
{
	[Test]
	public async Task PlanAsync_WhenAQueuedRemovalTheProviderAlreadyAgreesWith_DropsItWithoutRunningASync()
	{
		var stale = MediaId.From(new Guid(11, 0, 0, new byte[8]));
		var outbox = IWantlistOutbox.Mock();
		outbox.GetAsync(Any()).Returns([new WantlistOutboxEntry(stale, WantlistOperationAction.Remove, WantlistOutboxState.Pending, 0)]);
		var provider = IMediaSink.Mock();
		provider.GetWantlistAsync(Any()).Returns(WantlistStubs.NothingRemote());

		await WantlistStubs.Planner(provider, outbox).PlanAsync();

		outbox.DropAsync(Any<IEnumerable<MediaId>>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task PlanAsync_WhenTheOutboxAgreesWithTheProvider_LeavesItAlone()
	{
		var provider = IMediaSink.Mock();
		provider.GetWantlistAsync(Any()).Returns(WantlistStubs.NothingRemote());
		var outbox = WantlistStubs.EmptyOutbox();

		await WantlistStubs.Planner(provider, outbox).PlanAsync();

		outbox.DropAsync(Any<IEnumerable<MediaId>>(), Any()).WasNeverCalled();
	}
}
