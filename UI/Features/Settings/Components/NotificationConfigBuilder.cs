using Domain.Notifications;

namespace UI.Features.Settings.Components;

internal static class NotificationConfigBuilder
{
	public static WebhookConfig Webhook(string url, string signingSecret) => new(url.Trim(), signingSecret);

	public static PushoverConfig Pushover(string appToken, string userKey) => new(appToken.Trim(), userKey.Trim());
}
