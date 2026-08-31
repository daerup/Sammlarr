using Contracts.DI;
using Contracts.Notifications;
using Domain.Notifications;

namespace UI.Features.Settings.Components;

internal sealed class NotificationTester(IKeyedServiceIndex<NotificationChannel, INotificationChannel> channels)
{
	public Task<DeliveryResult> PingAsync(NotificationTarget target, CancellationToken cancellationToken = default) =>
		channels[target.Channel].SendAsync(PingContext(), target, NotificationTemplate.None, cancellationToken);

	private static NotificationContext PingContext() =>
		new(Guid.CreateVersion7(), "test.ping", DateTimeOffset.UtcNow, "Test notification", "This is a test notification from Sammlarr.", new NotificationData());
}
