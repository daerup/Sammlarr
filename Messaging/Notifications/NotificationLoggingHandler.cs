using Domain.Notifications.Events;
using Microsoft.Extensions.Logging;

namespace Messaging.Notifications;

public sealed class NotificationLoggingHandler
{
	public void Handle(WatchlistSyncedEvent message, ILogger<NotificationLoggingHandler> logger) => Log(message, logger);

	public void Handle(WatchlistItemsAddedEvent message, ILogger<NotificationLoggingHandler> logger) => Log(message, logger);

	public void Handle(WatchlistItemsRemovedEvent message, ILogger<NotificationLoggingHandler> logger) => Log(message, logger);

	public void Handle(TestPingEvent message, ILogger<NotificationLoggingHandler> logger) => Log(message, logger);

	private static void Log(BaseNotification message, ILogger logger) =>
		logger.LogInformation("Notification event {EventType} {EventId} for user {UserId}", message.Type, message.Id, message.UserId);
}
