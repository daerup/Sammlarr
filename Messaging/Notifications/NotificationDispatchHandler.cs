using Contracts.Feed;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Notifications;
using Domain.Notifications;
using Domain.Notifications.Events;
using Wolverine;

namespace Messaging.Notifications;

public static class NotificationDispatchHandler
{
	public static Task<OutgoingMessages> Handle(
		WatchlistSyncedEvent message,
		IUserScope userScope,
		INotificationSubscriptionRepository subscriptions,
		IMediaRepository mediaRepository,
		IFeedRepository feedRepository,
		CancellationToken cancellationToken
	) => FanOutAsync(message, userScope, subscriptions, mediaRepository, feedRepository, cancellationToken);

	public static Task<OutgoingMessages> Handle(
		ProviderRateLimitedEvent message,
		IUserScope userScope,
		INotificationSubscriptionRepository subscriptions,
		IMediaRepository mediaRepository,
		IFeedRepository feedRepository,
		CancellationToken cancellationToken
	) => FanOutAsync(message, userScope, subscriptions, mediaRepository, feedRepository, cancellationToken);

	public static async Task<DeliverNotification> Handle(
		TestPingEvent message,
		IMediaRepository mediaRepository,
		IFeedRepository feedRepository,
		CancellationToken cancellationToken
	)
	{
		var context = await NotificationContextFactory.BuildAsync(message, mediaRepository, feedRepository, cancellationToken);
		return new DeliverNotification(context, message.TargetId, message.UserId, NotificationTemplate.None);
	}

	private static async Task<OutgoingMessages> FanOutAsync(
		BaseNotification message,
		IUserScope userScope,
		INotificationSubscriptionRepository subscriptions,
		IMediaRepository mediaRepository,
		IFeedRepository feedRepository,
		CancellationToken cancellationToken
	)
	{
		using var scope = await userScope.AsAsync(message.UserId, cancellationToken);
		var matching = await subscriptions.GetForEventTypeAsync(message.Type, cancellationToken);
		if (matching.Count == 0)
		{
			return [];
		}

		var context = await NotificationContextFactory.BuildAsync(message, mediaRepository, feedRepository, cancellationToken);
		return [.. matching.Select(subscription => new DeliverNotification(context, subscription.TargetId, message.UserId, subscription.Template))];
	}
}
