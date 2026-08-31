using Domain.Notifications;

namespace Contracts.Notifications;

public interface INotificationChannel
{
	NotificationChannel Kind { get; }

	Task<DeliveryResult> SendAsync(
		NotificationContext context,
		NotificationTarget target,
		NotificationTemplate template,
		CancellationToken cancellationToken = default
	);
}
