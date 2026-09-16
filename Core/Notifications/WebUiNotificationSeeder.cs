using Contracts.Notifications;
using Domain.Notifications;

namespace Core.Notifications;

internal sealed class WebUiNotificationSeeder(INotificationTargetRepository targets, INotificationSubscriptionRepository subscriptions)
{
	private static readonly NotificationEventType[] subscribedEvents = [NotificationEventType.ProviderRateLimited];

	private static readonly NotificationTemplate template = NotificationTemplate.None with
	{
		Title = Some.Of("{{ data.provider }} is rate limiting"),
		Message = Some.Of("{{ summary }}"),
		Level = Some.Of(NotificationLevel.Warning),
	};

	public async Task SeedAsync(CancellationToken cancellationToken = default)
	{
		var targetId = await targets.CreateAsync(new NotificationTarget(Guid.Empty, "Sammlarr", true, new WebUiConfig()), cancellationToken);
		await subscriptions.AddAsync(targetId, subscribedEvents, template, cancellationToken);
	}
}
