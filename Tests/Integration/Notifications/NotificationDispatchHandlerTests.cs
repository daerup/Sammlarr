using Contracts.Feed;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Notifications;
using Domain.Feed;
using Domain.Identity;
using Domain.Media;
using Domain.Notifications;
using Domain.Providers;
using Messaging.Notifications;
using Shared.Extensions;
using TestInfrastructure;

namespace Integration.Tests.Notifications;

public class NotificationDispatchHandlerTests
{
	private static readonly UserId User = UserId.From(Guid.NewGuid());

	[Test]
	public async Task Handle_WhenAWantlistSyncs_FansOutOneMessagePerMatchingSubscription()
	{
		var targetA = Guid.NewGuid();
		var targetB = Guid.NewGuid();
		var subscriptions = SubscriptionsFor(
			NotificationEventType.WantlistSyncCompleted,
			new NotificationSubscription(Guid.NewGuid(), targetA, [NotificationEventType.WantlistSyncCompleted], NotificationTemplate.None),
			new NotificationSubscription(Guid.NewGuid(), targetB, [NotificationEventType.WantlistSyncCompleted], NotificationTemplate.None)
		);
		var @event = new WantlistSyncCompletedNotification(User, DateTimeOffset.UtcNow, [], [], [], [], []);

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
	public async Task Handle_WhenAWantlistSyncs_ConsolidatesPulledAndPushedSortedByDescriptor()
	{
		var pulledAdd = MediaId.From(Guid.NewGuid());
		var pushedRemove = MediaId.From(Guid.NewGuid());
		var subscriptions = SubscriptionsFor(
			NotificationEventType.WantlistSyncCompleted,
			new NotificationSubscription(Guid.NewGuid(), Guid.NewGuid(), [NotificationEventType.WantlistSyncCompleted], NotificationTemplate.None)
		);
		var media = Media(
			new CachedMedia(new IdentifiedMedia(new MediaIdentity(pulledAdd, []), new MovieDetails { Title = "Pulled Movie" }), DateTimeOffset.UtcNow),
			new CachedMedia(new IdentifiedMedia(new MediaIdentity(pushedRemove, []), new MovieDetails { Title = "Pushed Movie" }), DateTimeOffset.UtcNow)
		);

		var messages = await NotificationDispatchHandler.Handle(
			new WantlistSyncCompletedNotification(User, DateTimeOffset.UtcNow, [pulledAdd], [], [], [pushedRemove], []),
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
	public async Task Handle_WhenAWantlistSyncFails_DeliversTheReasonAsBothSummaryAndData()
	{
		var targetId = Guid.NewGuid();
		var subscriptions = SubscriptionsFor(
			NotificationEventType.WantlistSyncFailed,
			new NotificationSubscription(Guid.NewGuid(), targetId, [NotificationEventType.WantlistSyncFailed], NotificationTemplate.None)
		);

		var messages = await NotificationDispatchHandler.Handle(
			new WantlistSyncFailedNotification(User, DateTimeOffset.UtcNow, "Couldn't reach Plex"),
			Scope().Object,
			subscriptions.Object,
			Media().Object,
			Feed().Object,
			CancellationToken.None
		);

		var delivery = messages.OfType<DeliverNotification>().Single();
		await Assert.That(delivery.TargetId).IsEqualTo(targetId);
		await Assert.That(delivery.Context.Summary).IsEqualTo("Couldn't reach Plex");
		await Assert.That(delivery.Context.Data.Reason).IsEqualTo("Couldn't reach Plex");
		await Assert.That(delivery.Context.Data.Media).IsNull();
	}

	[Test]
	public async Task Handle_WhenAProviderIsRateLimited_DeliversTheProviderAndRetryDelay()
	{
		var targetId = Guid.NewGuid();
		var subscriptions = SubscriptionsFor(
			NotificationEventType.ProviderRateLimited,
			new NotificationSubscription(Guid.NewGuid(), targetId, [NotificationEventType.ProviderRateLimited], NotificationTemplate.None)
		);

		var messages = await NotificationDispatchHandler.Handle(
			new ProviderRateLimitedNotification(User, DateTimeOffset.UtcNow, MediaProvider.Plex, TimeSpan.FromSeconds(30)),
			Scope().Object,
			subscriptions.Object,
			Media().Object,
			Feed().Object,
			CancellationToken.None
		);

		var delivery = messages.OfType<DeliverNotification>().Single();
		await Assert.That(delivery.TargetId).IsEqualTo(targetId);
		await Assert.That(delivery.UserId).IsEqualTo(User);
		await Assert.That(delivery.Context.Data.Provider).IsEqualTo(MediaProvider.Plex);
		await Assert.That(delivery.Context.Data.RetryAfter).IsEqualTo(TimeSpan.FromSeconds(30));
	}

	[Test]
	public async Task Handle_WhenItIsATestPing_RoutesDirectlyWithoutASubscriptionLookup()
	{
		var targetId = Guid.NewGuid();

		var message = await NotificationDispatchHandler.Handle(
			new TestPingNotification(User, DateTimeOffset.UtcNow, targetId),
			Media().Object,
			Feed().Object,
			CancellationToken.None
		);

		await Assert.That(message.TargetId).IsEqualTo(targetId);
		await Assert.That(message.UserId).IsEqualTo(User);
		await Assert.That(message.Context.Type).IsEqualTo(NotificationEventType.TestPing);
	}

	[Test]
	public async Task Handle_WhenFanningOut_EstablishesTheUserScopeForTheOwner()
	{
		var scope = Scope();

		await NotificationDispatchHandler.Handle(
			new WantlistSyncCompletedNotification(User, DateTimeOffset.UtcNow, [], [], [], [], []),
			scope.Object,
			SubscriptionsFor(NotificationEventType.WantlistSyncCompleted).Object,
			Media().Object,
			Feed().Object,
			CancellationToken.None
		);

		scope.AsAsync(User, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task Handle_WhenNoSubscriptionMatches_ProducesNoMessages()
	{
		var media = Media();

		var messages = await NotificationDispatchHandler.Handle(
			new WantlistSyncCompletedNotification(User, DateTimeOffset.UtcNow, [MediaId.From(Guid.NewGuid())], [], [], [], []),
			Scope().Object,
			SubscriptionsFor(NotificationEventType.WantlistSyncCompleted).Object,
			media.Object,
			Feed().Object,
			CancellationToken.None
		);

		await Assert.That(messages).IsEmpty();
		media.GetAsync(Any<IReadOnlyList<MediaId>>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task Handle_WhenSeveralTargetsMatch_ResolvesMediaOnceAndSharesTheContext()
	{
		var id = MediaId.From(Guid.NewGuid());
		var subscriptions = SubscriptionsFor(
			NotificationEventType.WantlistSyncCompleted,
			new NotificationSubscription(Guid.NewGuid(), Guid.NewGuid(), [NotificationEventType.WantlistSyncCompleted], NotificationTemplate.None),
			new NotificationSubscription(Guid.NewGuid(), Guid.NewGuid(), [NotificationEventType.WantlistSyncCompleted], NotificationTemplate.None)
		);
		var media = Media(new CachedMedia(new IdentifiedMedia(new MediaIdentity(id, []), new MovieDetails { Title = "The Matrix" }), DateTimeOffset.UtcNow));

		var messages = await NotificationDispatchHandler.Handle(
			new WantlistSyncCompletedNotification(User, DateTimeOffset.UtcNow, [id], [], [], [], []),
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

	[Test]
	public async Task Handle_WhenAWantlistSyncs_TitlesTheContextFromTheEventTypeDescription()
	{
		var subscriptions = SubscriptionsFor(
			NotificationEventType.WantlistSyncCompleted,
			new NotificationSubscription(Guid.NewGuid(), Guid.NewGuid(), [NotificationEventType.WantlistSyncCompleted], NotificationTemplate.None)
		);

		var messages = await NotificationDispatchHandler.Handle(
			new WantlistSyncCompletedNotification(User, DateTimeOffset.UtcNow, [], [], [], [], []),
			Scope().Object,
			subscriptions.Object,
			Media().Object,
			Feed().Object,
			CancellationToken.None
		);

		var context = messages.OfType<DeliverNotification>().Single().Context;
		await Assert.That(context.Title).IsEqualTo(NotificationEventType.WantlistSyncCompleted.GetDescription());
		await Assert.That(context.Summary).IsEqualTo("Pulled 0, pushed 0.");
	}

	[Test]
	public async Task Handle_WhenAFeedRunCompletes_NamesTheFeedInTheContext()
	{
		var feed = TestFeeds.Feed(Guid.NewGuid(), "Comedy");
		var subscriptions = SubscriptionsFor(
			NotificationEventType.FeedRunCompleted,
			new NotificationSubscription(Guid.NewGuid(), Guid.NewGuid(), [NotificationEventType.FeedRunCompleted], NotificationTemplate.None)
		);

		var messages = await NotificationDispatchHandler.Handle(
			new FeedRunCompletedNotification(User, DateTimeOffset.UtcNow, feed.Id, 2, 1),
			Scope().Object,
			subscriptions.Object,
			Media().Object,
			Finding(Some.Of(feed)).Object,
			CancellationToken.None
		);

		var data = messages.OfType<DeliverNotification>().Single().Context.Data;
		await Assert.That(data.Feed!.Name).IsEqualTo("Comedy");
		await Assert.That(data.Added).IsEqualTo(2);
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

	private static Mock<IFeedRepository> Feed() => Finding(new None());

	private static Mock<IFeedRepository> Finding(Optional<Domain.Feed.Feed> feed)
	{
		var repository = IFeedRepository.Mock();
		repository.FindAsync(Any<FeedId>(), Any()).Returns(feed);
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
