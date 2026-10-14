using Contracts;
using Contracts.DI;
using Contracts.Notifications;
using Domain.Notifications;

namespace UI.Features.Settings.Components.Notifications;

internal sealed class NotificationTester(IKeyedServiceIndex<NotificationChannel, INotificationChannel> channels, IDateTimeProvider clock)
{
	private static readonly NotificationTemplate pingTemplate = new(new None(), Some.Of("{{ title }}"), Some.Of("{{ summary }}"), new None(), new None());

	public Task<DeliveryResult> PingAsync(NotificationTarget target, CancellationToken cancellationToken = default) =>
		channels[target.Channel].SendAsync(PingContext(), target, pingTemplate, cancellationToken);

	private NotificationContext PingContext() =>
		new(
			Guid.CreateVersion7(),
			NotificationEventType.TestPing,
			NotificationLevel.Info,
			clock.Now,
			"Test notification",
			"This is a test notification from Sammlarr.",
			new NotificationData()
		);
}
