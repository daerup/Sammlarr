using Contracts.Jobs;
using Core.Jobs;
using Domain.Feed;
using Domain.Jobs;
using Domain.Wantlist.Sync;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Tests.Jobs;

public class JobHandlerTests
{
	[Test]
	public async Task DispatchAsync_ResolvesTheHandlerForTheRequestsRuntimeType()
	{
		var feedId = FeedId.From(Guid.NewGuid());
		var (services, feeds, syncs) = BuildServices();

		await JobRunner.DispatchAsync(services, new RunFeed(feedId, new Manual()), CancellationToken.None);

		await Assert.That(feeds.Received?.Id).IsEqualTo(feedId);
		await Assert.That(syncs.Received).IsNull();
	}

	[Test]
	public async Task DispatchAsync_OfAWantlistSync_ReachesTheSyncHandler()
	{
		var (services, feeds, syncs) = BuildServices();

		await JobRunner.DispatchAsync(services, new SyncWantlist(new ManualTrigger()), CancellationToken.None);

		await Assert.That(syncs.Received).IsNotNull();
		await Assert.That(feeds.Received).IsNull();
	}

	[Test]
	public async Task DispatchAsync_CarriesTheTriggerSoRunHistoryCanTellSchedulingApart()
	{
		var (services, feeds, _) = BuildServices();

		await JobRunner.DispatchAsync(services, new RunFeed(FeedId.From(Guid.NewGuid()), new Scheduled("0 0 * * * *")), CancellationToken.None);

		await Assert.That(feeds.Received!.Value.Trigger is Scheduled).IsTrue();
	}

	[Test]
	public async Task DispatchAsync_WhenNoHandlerIsRegistered_Throws()
	{
		var services = new ServiceCollection().BuildServiceProvider();

		await Assert
			.That(async () => await JobRunner.DispatchAsync(services, new SyncWantlist(new ManualTrigger()), CancellationToken.None))
			.Throws<InvalidOperationException>();
	}

	private static (IServiceProvider Services, RecordingFeedHandler Feeds, RecordingSyncHandler Syncs) BuildServices()
	{
		var feeds = new RecordingFeedHandler();
		var syncs = new RecordingSyncHandler();
		var services = new ServiceCollection();
		services.AddSingleton<IJobHandler<RunFeed>>(feeds);
		services.AddSingleton<IJobHandler<SyncWantlist>>(syncs);

		return (services.BuildServiceProvider(), feeds, syncs);
	}

	private sealed class RecordingFeedHandler : IJobHandler<RunFeed>
	{
		public RunFeed? Received { get; private set; }

		public Task HandleAsync(RunFeed request, CancellationToken cancellationToken = default)
		{
			Received = request;
			return Task.CompletedTask;
		}
	}

	private sealed class RecordingSyncHandler : IJobHandler<SyncWantlist>
	{
		public SyncWantlist? Received { get; private set; }

		public Task HandleAsync(SyncWantlist request, CancellationToken cancellationToken = default)
		{
			Received = request;
			return Task.CompletedTask;
		}
	}
}
