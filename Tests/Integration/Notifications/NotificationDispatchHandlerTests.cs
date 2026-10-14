using Contracts.Feed;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Notifications;
using Domain.Feed;
using Domain.Identity;
using Domain.Media;
using Domain.Notifications;
using Domain.Providers;
using Harness;
using Integration.Tests.Messaging;
using Messaging.Notifications;
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
			MessagingHost.PassingUserScope().Object,
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
		var pulledAdd = MediaId.New();
		var pushedRemove = MediaId.New();
		var subscriptions = SubscriptionsFor(
			NotificationEventType.WantlistSyncCompleted,
			new NotificationSubscription(Guid.NewGuid(), Guid.NewGuid(), [NotificationEventType.WantlistSyncCompleted], NotificationTemplate.None)
		);
		var media = Media(Stored(pulledAdd, "Pulled Movie"), Stored(pushedRemove, "Pushed Movie"));

		var messages = await NotificationDispatchHandler.Handle(
			new WantlistSyncCompletedNotification(User, DateTimeOffset.UtcNow, [pulledAdd], [], [], [pushedRemove], []),
			MessagingHost.PassingUserScope().Object,
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
			MessagingHost.PassingUserScope().Object,
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
			new ProviderRateLimitedNotification(User, DateTimeOffset.UtcNow, RemoteService.Plex, TimeSpan.FromSeconds(30)),
			MessagingHost.PassingUserScope().Object,
			subscriptions.Object,
			Media().Object,
			Feed().Object,
			CancellationToken.None
		);

		var delivery = messages.OfType<DeliverNotification>().Single();
		await Assert.That(delivery.TargetId).IsEqualTo(targetId);
		await Assert.That(delivery.UserId).IsEqualTo(User);
		await Assert.That(delivery.Context.Data.Provider).IsEqualTo(RemoteService.Plex);
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
	public async Task Handle_ForOneOwnersEvent_DeliversOnlyToTheTargetThatOwnerSubscribed()
	{
		await using var host = new SammlarrTestHost();
		var (ann, annsTarget) = await SubscribedAsync(host, "Ann");
		await SubscribedAsync(host, "Ben");

		var messages = await NotificationDispatchHandler.Handle(
			new WantlistSyncCompletedNotification(ann, DateTimeOffset.UtcNow, [], [], [], [], []),
			host.Resolve<IUserScope>(),
			host.Resolve<INotificationSubscriptionRepository>(),
			Media().Object,
			Feed().Object,
			CancellationToken.None
		);

		await Assert.That(messages.OfType<DeliverNotification>().Select(delivery => delivery.TargetId)).IsEquivalentTo([annsTarget]);
	}

	[Test]
	public async Task Handle_WhenNoSubscriptionMatches_ProducesNoMessages()
	{
		var messages = await NotificationDispatchHandler.Handle(
			new WantlistSyncCompletedNotification(User, DateTimeOffset.UtcNow, [MediaId.New()], [], [], [], []),
			MessagingHost.PassingUserScope().Object,
			SubscriptionsFor(NotificationEventType.WantlistSyncCompleted).Object,
			Media().Object,
			Feed().Object,
			CancellationToken.None
		);

		await Assert.That(messages).IsEmpty();
	}

	[Test]
	public async Task Handle_WhenSeveralTargetsMatch_DescribesTheTitleInEachDelivery()
	{
		var id = MediaId.New();
		var subscriptions = SubscriptionsFor(
			NotificationEventType.WantlistSyncCompleted,
			new NotificationSubscription(Guid.NewGuid(), Guid.NewGuid(), [NotificationEventType.WantlistSyncCompleted], NotificationTemplate.None),
			new NotificationSubscription(Guid.NewGuid(), Guid.NewGuid(), [NotificationEventType.WantlistSyncCompleted], NotificationTemplate.None)
		);
		var media = Media(Stored(id, "The Matrix"));

		var messages = await NotificationDispatchHandler.Handle(
			new WantlistSyncCompletedNotification(User, DateTimeOffset.UtcNow, [id], [], [], [], []),
			MessagingHost.PassingUserScope().Object,
			subscriptions.Object,
			media.Object,
			Feed().Object,
			CancellationToken.None
		);

		var deliveries = messages.OfType<DeliverNotification>().ToList();
		await Assert.That(deliveries).Count().IsEqualTo(2);
		foreach (var delivery in deliveries)
		{
			await Assert.That(delivery.Context.Data.Media!.Single().Title).IsEqualTo("The Matrix");
		}
	}

	[Test]
	public async Task Handle_ForStoredTitles_DescribesThemFromTheirCachedDetailsOrByTheirTypeAlone()
	{
		var described = MediaId.New();
		var bare = MediaId.New();
		var subscriptions = SubscriptionsFor(
			NotificationEventType.WantlistSyncCompleted,
			new NotificationSubscription(Guid.NewGuid(), Guid.NewGuid(), [NotificationEventType.WantlistSyncCompleted], NotificationTemplate.None)
		);
		var details = new MovieDetails { Title = "The Matrix", ReleaseDate = new DateOnly(1999, 3, 31) };
		var media = Media(
			new StoredMedia(
				new MediaIdentity(described, [TmdbId.From("603", MediaType.Movie)]),
				Some.Of(new CachedDetails(details, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, []))
			),
			new StoredMedia(new MediaIdentity(bare, [TmdbId.From("1396", MediaType.Show)]), new None())
		);

		var messages = await NotificationDispatchHandler.Handle(
			new WantlistSyncCompletedNotification(User, DateTimeOffset.UtcNow, [described, bare], [], [], [], []),
			MessagingHost.PassingUserScope().Object,
			subscriptions.Object,
			media.Object,
			Feed().Object,
			CancellationToken.None
		);

		var items = messages.OfType<DeliverNotification>().Single().Context.Data.Media!;
		await Assert
			.That(items)
			.IsEquivalentTo([
				new NotificationMedia(described.ToString(), "The Matrix", MediaType.Movie, 1999, "Added locally"),
				new NotificationMedia(bare.ToString(), null, MediaType.Show, null, "Added locally"),
			]);
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
			MessagingHost.PassingUserScope().Object,
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
		var feed = TestFeeds.Feed(Guid.NewGuid());
		var subscriptions = SubscriptionsFor(
			NotificationEventType.FeedRunCompleted,
			new NotificationSubscription(Guid.NewGuid(), Guid.NewGuid(), [NotificationEventType.FeedRunCompleted], NotificationTemplate.None)
		);

		var messages = await NotificationDispatchHandler.Handle(
			new FeedRunCompletedNotification(User, DateTimeOffset.UtcNow, feed.Id, 2, 1),
			MessagingHost.PassingUserScope().Object,
			subscriptions.Object,
			Media().Object,
			Finding(Some.Of(feed)).Object,
			CancellationToken.None
		);

		var data = messages.OfType<DeliverNotification>().Single().Context.Data;
		await Assert.That(data.Feed!.Name).IsEqualTo("Comedy");
		await Assert.That(data.Added).IsEqualTo(2);
	}

	[Test]
	public async Task Handle_WhenTheFeedOfAFailedRunIsGone_SendsTheContextWithoutAFeed()
	{
		var subscriptions = SubscriptionsFor(
			NotificationEventType.FeedRunFailed,
			new NotificationSubscription(Guid.NewGuid(), Guid.NewGuid(), [NotificationEventType.FeedRunFailed], NotificationTemplate.None)
		);

		var messages = await NotificationDispatchHandler.Handle(
			new FeedRunFailedNotification(User, DateTimeOffset.UtcNow, FeedId.From(Guid.NewGuid()), "HTTP 404"),
			MessagingHost.PassingUserScope().Object,
			subscriptions.Object,
			Media().Object,
			Feed().Object,
			CancellationToken.None
		);

		var data = messages.OfType<DeliverNotification>().Single().Context.Data;
		await Assert.That(data.Feed).IsNull();
		await Assert.That(data.Reason).IsEqualTo("HTTP 404");
	}

	private static Mock<INotificationSubscriptionRepository> SubscriptionsFor(NotificationEventType eventType, params NotificationSubscription[] subscriptions)
	{
		var repository = INotificationSubscriptionRepository.Mock();
		repository.GetForEventTypeAsync(eventType, Any()).Returns(subscriptions);
		return repository;
	}

	private static Mock<IMediaRepository> Media(params StoredMedia[] stored)
	{
		var repository = IMediaRepository.Mock();
		repository.GetAsync(Any<IReadOnlyList<MediaId>>(), Any()).Returns(stored);
		return repository;
	}

	private static StoredMedia Stored(MediaId id, string title)
	{
		return new(
			new MediaIdentity(id, [TmdbId.From(id.ToString(), MediaType.Movie)]),
			Some.Of(new CachedDetails(new MovieDetails { Title = title }, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, []))
		);
	}

	private static async Task<(UserId Owner, Guid Target)> SubscribedAsync(SammlarrTestHost host, string name)
	{
		await host.SignInAs(name);
		var target = await host.Resolve<INotificationTargetRepository>()
			.CreateAsync(new NotificationTarget(Guid.Empty, name, true, new PushoverConfig("token", "key")));
		await host.Resolve<INotificationSubscriptionRepository>().AddAsync(target, [NotificationEventType.WantlistSyncCompleted], NotificationTemplate.None);
		return (host.CurrentUser.Id, target);
	}

	private static Mock<IFeedRepository> Feed() => Finding(new None());

	private static Mock<IFeedRepository> Finding(Optional<Feed> feed)
	{
		var repository = IFeedRepository.Mock();
		repository.FindAsync(Any<FeedId>(), Any()).Returns(feed);
		return repository;
	}
}
