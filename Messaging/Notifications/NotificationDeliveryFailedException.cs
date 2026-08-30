namespace Messaging.Notifications;

internal sealed class NotificationDeliveryFailedException(Guid targetId, string reason)
	: Exception($"Notification delivery to target {targetId} failed: {reason}");
