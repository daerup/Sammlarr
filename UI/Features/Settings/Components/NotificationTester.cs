using Contracts.DI;
using Contracts.Notifications;
using Domain.Notifications;
using Shared;

namespace UI.Features.Settings.Components;

internal sealed class NotificationTester(IKeyedServiceIndex<NotificationChannel, INotificationChannel> channels)
{
	private static readonly NotificationTemplate pingTemplate = new(new None(), Some.Of("{{ title }}"), Some.Of("{{ summary }}"), new None());

	public Task<DeliveryResult> PingAsync(NotificationTarget target, CancellationToken cancellationToken = default) =>
		channels[target.Channel].SendAsync(PingContext(), target, pingTemplate, cancellationToken);

	private static NotificationContext PingContext() =>
		new(Guid.CreateVersion7(), "test.ping", DateTimeOffset.UtcNow, "Test notification", "This is a test notification from Sammlarr.", new NotificationData());
}
