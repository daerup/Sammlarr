using Contracts.DI;
using Contracts.Identity;
using Contracts.Notifications;
using Domain.Notifications;

namespace Messaging.Notifications;

public static class NotificationDeliveryHandler
{
	public static async Task Handle(
		DeliverNotification message,
		IUserScope userScope,
		INotificationTargetRepository targets,
		IKeyedServiceIndex<NotificationChannel, INotificationChannel> channels,
		CancellationToken cancellationToken
	)
	{
		using var scope = await userScope.AsAsync(message.UserId, cancellationToken);

		var target = await targets.FindAsync(message.TargetId, cancellationToken);
		if (target is null || !target.Enabled)
		{
			return;
		}

		var result = await channels[target.Channel].SendAsync(message.Context, target, cancellationToken);
		if (result is DeliveryTransient transient)
		{
			throw new NotificationDeliveryFailedException(message.TargetId, transient.Reason);
		}
	}
}
