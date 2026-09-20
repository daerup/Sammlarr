using Contracts.Notifications;
using Domain.Notifications;

namespace Core.Notifications;

internal sealed class WebUiNotificationSeeder(INotificationTargetRepository targets, INotificationSubscriptionRepository subscriptions)
{
	private static readonly NotificationEventType[] subscribedEvents =
	[
		NotificationEventType.ProviderRateLimited,
		NotificationEventType.WatchlistSynced,
		NotificationEventType.WatchlistSyncFailed,
	];

	public async Task SeedAsync(CancellationToken cancellationToken = default)
	{
		var targetId = await targets.CreateAsync(new NotificationTarget(Guid.Empty, "Sammlarr", true, new WebUiConfig()), cancellationToken);
		await subscriptions.AddAsync(targetId, subscribedEvents, NotificationTemplate.None, cancellationToken);
	}
}
