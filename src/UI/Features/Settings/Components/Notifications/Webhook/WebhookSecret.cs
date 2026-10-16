using System.Security.Cryptography;

namespace UI.Features.Settings.Components.Notifications.Webhook;

internal static class WebhookSecret
{
	public static string Generate() => "whsec_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
