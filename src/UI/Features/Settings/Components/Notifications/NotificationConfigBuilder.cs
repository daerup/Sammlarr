using Domain.Notifications;

namespace UI.Features.Settings.Components.Notifications;

internal static class NotificationConfigBuilder
{
	public static WebhookConfig Webhook(string url, string signingSecret) => new(new Secret(url.Trim()), new Secret(signingSecret));

	public static PushoverConfig Pushover(string appToken, string userKey) => new(new Secret(appToken.Trim()), new Secret(userKey.Trim()));

	public static WebUiConfig WebUi() => new();
}
