namespace API.Notifications.Webhook;

internal sealed class WebhookBlockedException(string message) : Exception(message);
