using Domain.Notifications;

namespace Contracts.Notifications;

public interface INotificationChannel
{
	Task<DeliveryResult> SendAsync(
		NotificationContext context,
		NotificationTarget target,
		NotificationTemplate template,
		CancellationToken cancellationToken = default
	);
}
