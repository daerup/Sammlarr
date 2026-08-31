using Domain.Notifications;

namespace UI.Features.Settings.Components;

internal static class NotificationConfigBuilder
{
	public static WebhookConfig Webhook(string url, string signingSecret) => new(url.Trim(), signingSecret);

	public static PushoverConfig Pushover(string? appToken, string? userKey, PushoverConfig? existing) =>
		new(Keep(appToken, existing?.AppToken), Keep(userKey, existing?.UserKey));

	private static string Keep(string? entered, string? existing) => string.IsNullOrWhiteSpace(entered) ? existing ?? string.Empty : entered.Trim();
}
