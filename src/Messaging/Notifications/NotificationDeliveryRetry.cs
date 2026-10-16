namespace Messaging.Notifications;

internal static class NotificationDeliveryRetry
{
	internal static readonly TimeSpan[] Cooldowns = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1)];

	internal static readonly int MaxAttempts = Cooldowns.Length + 1;
}
