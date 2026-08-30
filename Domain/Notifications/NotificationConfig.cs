namespace Domain.Notifications;

public union NotificationConfig(WebhookConfig, PushoverConfig);

public readonly record struct WebhookConfig(string Url, string SigningSecret, string? BodyTemplate = null);

public readonly record struct PushoverConfig(string AppToken, string UserKey, string? TitleTemplate = null, string? MessageTemplate = null);
