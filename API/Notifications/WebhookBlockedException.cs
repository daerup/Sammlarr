namespace API.Notifications;

internal sealed class WebhookBlockedException(string message) : Exception(message);
