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

		await targets
			.FindAsync(message.TargetId, cancellationToken)
			.When(target => target.Enabled)
			.Apply(target => DeliverAsync(message, target, envelope.Attempts, channels, outcomes, clock, cancellationToken));
	}

	private static async Task DeliverAsync(
		DeliverNotification message,
		NotificationTarget target,
		int attempts,
		IKeyedServiceIndex<NotificationChannel, INotificationChannel> channels,
		INotificationDeliveryOutcomeRepository outcomes,
		IDateTimeProvider clock,
		CancellationToken cancellationToken
	)
	{
		var result = await channels[target.Channel].SendAsync(message.Context, target, message.Template, cancellationToken);

		DeliveryOutcome outcome = result switch
		{
			DeliverySucceeded => new Delivered(),
			DeliveryUnrecoverable unrecoverable => new Failed(unrecoverable.Reason),
			DeliveryRetryable retryable when attempts >= NotificationDeliveryRetry.MaxAttempts => new Failed(retryable.Reason),
			DeliveryRetryable retryable => new Retrying(retryable.Reason, clock.Now + NotificationDeliveryRetry.Cooldowns[attempts - 1]),
		};

		await outcomes.RecordAsync(message.Context.Id, message.TargetId, message.Context.Type, outcome, cancellationToken);

		if (outcome is Retrying retrying)
		{
			throw new NotificationDeliveryFailedException(message.TargetId, retrying.Reason);
		}
	}
}
