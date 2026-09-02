using Contracts;
using Contracts.DI;
using Contracts.Identity;
using Contracts.Notifications;
using Domain.Notifications;
using Wolverine;

namespace Messaging.Notifications;

public static class NotificationDeliveryHandler
{
	public static async Task Handle(
		DeliverNotification message,
		Envelope envelope,
		IUserScope userScope,
		INotificationTargetRepository targets,
		IKeyedServiceIndex<NotificationChannel, INotificationChannel> channels,
		INotificationDeliveryOutcomeRepository outcomes,
		IDateTimeProvider clock,
		CancellationToken cancellationToken
	)
	{
		using var scope = await userScope.AsAsync(message.UserId, cancellationToken);

		var target = await targets.FindAsync(message.TargetId, cancellationToken);
		if (target is null || !target.Enabled)
		{
			return;
		}

		var result = await channels[target.Channel].SendAsync(message.Context, target, message.Template, cancellationToken);

		DeliveryOutcome outcome = result switch
		{
			DeliverySucceeded => new Delivered(),
			DeliveryUnrecoverable unrecoverable => new Failed(unrecoverable.Reason),
			DeliveryRetryable retryable when envelope.Attempts >= NotificationDeliveryRetry.MaxAttempts => new Failed(retryable.Reason),
			DeliveryRetryable retryable => new Retrying(retryable.Reason, clock.Now + NotificationDeliveryRetry.Cooldowns[envelope.Attempts - 1]),
		};

		await outcomes.RecordAsync(message.Context.Id, message.TargetId, message.Context.Type, outcome, cancellationToken);

		if (outcome is Retrying retrying)
		{
			throw new NotificationDeliveryFailedException(message.TargetId, retrying.Reason);
		}
	}
}
