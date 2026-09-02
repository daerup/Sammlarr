using Contracts.Feed;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Notifications;
using Domain.Identity;
using Domain.Media;
using Domain.Notifications;
using Domain.Notifications.Events;
using Messaging.Notifications;
using TestInfrastructure;

namespace Integration.Tests.Notifications;

public class NotificationDispatchHandlerTests
{
	private static readonly UserId User = UserId.From(Guid.NewGuid());

	[Test]
	public async Task Watchlist_synced_fans_out_one_message_per_matching_subscription()
	{
		var targetA = Guid.NewGuid();
		var targetB = Guid.NewGuid();
		var subscriptions = SubscriptionsFor(
			NotificationEventType.WatchlistSynced,
			new NotificationSubscription(Guid.NewGuid(), targetA, [NotificationEventType.WatchlistSynced], NotificationTemplate.None),
			new NotificationSubscription(Guid.NewGuid(), targetB, [NotificationEventType.WatchlistSynced], NotificationTemplate.None)
		);
		var @event = new WatchlistSyncedEvent(User, DateTimeOffset.UtcNow, [], [], [], []);

		var messages = await NotificationDispatchHandler.Handle(
			@event,
			Scope().Object,
			subscriptions.Object,
			Media().Object,
			Feed().Object,
			CancellationToken.None
		);

		var deliveries = messages.OfType<DeliverNotification>().ToList();
		await Assert.That(deliveries).Count().IsEqualTo(2);
		await Assert.That(deliveries.Select(delivery => delivery.TargetId)).Contains(targetA).And.Contains(targetB);
		await Assert.That(deliveries.All(delivery => delivery.Context.Id == @event.Id)).IsTrue();
		await Assert.That(deliveries.All(delivery => delivery.UserId == User)).IsTrue();
	}

	[Test]
	public async Task Watchlist_synced_consolidates_pulled_and_pushed_into_media_sorted_by_descriptor()
	{
		var pulledAdd = MediaId.From(Guid.NewGuid());
		var pushedRemove = MediaId.From(Guid.NewGuid());
		var subscriptions = SubscriptionsFor(
			NotificationEventType.WatchlistSynced,
			new NotificationSubscription(Guid.NewGuid(), Guid.NewGuid(), [NotificationEventType.WatchlistSynced], NotificationTemplate.None)
		);
		var media = Media(
			new CachedMedia(new PartialMovie { Identity = new MediaIdentity(pulledAdd, []), Title = "Pulled Movie" }, DateTimeOffset.UtcNow),
			new CachedMedia(new PartialMovie { Identity = new MediaIdentity(pushedRemove, []), Title = "Pushed Movie" }, DateTimeOffset.UtcNow)
		);

		var messages = await NotificationDispatchHandler.Handle(
			new WatchlistSyncedEvent(User, DateTimeOffset.UtcNow, [pulledAdd], [], [], [pushedRemove]),
			Scope().Object,
			subscriptions.Object,
			media.Object,
			Feed().Object,
			CancellationToken.None
		);

		var items = messages.OfType<DeliverNotification>().Single().Context.Data.Media!;
		await Assert.That(items).Count().IsEqualTo(2);
		await Assert.That(items[0].Descriptor).IsEqualTo("Added locally");
		await Assert.That(items[0].Title).IsEqualTo("Pulled Movie");
		await Assert.That(items[1].Descriptor).IsEqualTo("Removed remotely");
		await Assert.That(items[1].Title).IsEqualTo("Pushed Movie");
	}

	[Test]
	public async Task Test_ping_routes_directly_to_its_target_without_a_subscription_lookup()
	{
		var targetId = Guid.NewGuid();

		var message = await NotificationDispatchHandler.Handle(
			new TestPingEvent(User, DateTimeOffset.UtcNow, targetId),
			Media().Object,
			Feed().Object,
			CancellationToken.None
		);

		await Assert.That(message.TargetId).IsEqualTo(targetId);
		await Assert.That(message.UserId).IsEqualTo(User);
		await Assert.That(message.Context.Type).IsEqualTo(NotificationEventType.TestPing);
	}

	[Test]
	public async Task Fan_out_establishes_the_user_scope_for_the_event_owner()
	{
		var scope = Scope();

		await NotificationDispatchHandler.Handle(
			new WatchlistSyncedEvent(User, DateTimeOffset.UtcNow, [], [], [], []),
			scope.Object,
			SubscriptionsFor(NotificationEventType.WatchlistSynced).Object,
			Media().Object,
			Feed().Object,
			CancellationToken.None
		);

		scope.AsAsync(User, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task No_matching_subscriptions_produces_no_messages()
	{
		var media = Media();

		var messages = await NotificationDispatchHandler.Handle(
			new WatchlistSyncedEvent(User, DateTimeOffset.UtcNow, [MediaId.From(Guid.NewGuid())], [], [], []),
			Scope().Object,
			SubscriptionsFor(NotificationEventType.WatchlistSynced).Object,
			media.Object,
			Feed().Object,
			CancellationToken.None
		);

		await Assert.That(messages).IsEmpty();
		media.GetAsync(Any<IReadOnlyList<MediaId>>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task Media_is_resolved_once_and_the_enriched_context_is_shared_across_targets()
	{
		var id = MediaId.From(Guid.NewGuid());
		var subscriptions = SubscriptionsFor(
			NotificationEventType.WatchlistSynced,
			new NotificationSubscription(Guid.NewGuid(), Guid.NewGuid(), [NotificationEventType.WatchlistSynced], NotificationTemplate.None),
			new NotificationSubscription(Guid.NewGuid(), Guid.NewGuid(), [NotificationEventType.WatchlistSynced], NotificationTemplate.None)
		);
		var media = Media(new CachedMedia(new PartialMovie { Identity = new MediaIdentity(id, []), Title = "The Matrix" }, DateTimeOffset.UtcNow));

		var messages = await NotificationDispatchHandler.Handle(
			new WatchlistSyncedEvent(User, DateTimeOffset.UtcNow, [id], [], [], []),
			Scope().Object,
			subscriptions.Object,
			media.Object,
			Feed().Object,
			CancellationToken.None
		);

		var deliveries = messages.OfType<DeliverNotification>().ToList();
		await Assert.That(deliveries).Count().IsEqualTo(2);
		media.GetAsync(Any<IReadOnlyList<MediaId>>(), Any()).WasCalled(Times.Once);
		await Assert.That(deliveries[0].Context).IsSameReferenceAs(deliveries[1].Context);
		await Assert.That(deliveries[0].Context.Data.Media!.Single().Title).IsEqualTo("The Matrix");
	}

	private static Mock<INotificationSubscriptionRepository> SubscriptionsFor(NotificationEventType eventType, params NotificationSubscription[] subscriptions)
	{
		var repository = INotificationSubscriptionRepository.Mock();
		repository.GetForEventTypeAsync(eventType, Any()).Returns((IReadOnlyList<NotificationSubscription>)subscriptions);
		return repository;
	}

	private static Mock<IMediaRepository> Media(params CachedMedia[] cached)
	{
		var repository = IMediaRepository.Mock();
		repository.GetAsync(Any<IReadOnlyList<MediaId>>(), Any()).Returns((IReadOnlyList<CachedMedia>)cached);
		return repository;
	}

	private static Mock<IFeedRepository> Feed()
	{
		var repository = IFeedRepository.Mock();
		repository.GetFeedByIdAsync(Any(), Any()).Returns((Domain.Feed.Feed?)null);
		return repository;
	}

	private static Mock<IUserScope> Scope()
	{
		var scope = IUserScope.Mock();
		scope.AsAsync(Any<UserId>(), Any()).Returns(new NoopDisposable());
		return scope;
	}

	private sealed class NoopDisposable : IDisposable
	{
		public void Dispose() { }
	}
}
