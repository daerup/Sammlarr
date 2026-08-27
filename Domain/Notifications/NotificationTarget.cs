namespace Domain.Notifications;

public sealed record NotificationTarget(Guid Id, string Name, bool Enabled, NotificationConfig Config)
{
	public NotificationChannel Channel =>
		Config switch
		{
			WebhookConfig => NotificationChannel.Webhook,
			PushoverConfig => NotificationChannel.Pushover,
		};
}
