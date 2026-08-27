namespace Domain.Notifications;

public union NotificationConfig(WebhookConfig, PushoverConfig);

public readonly record struct WebhookConfig(string Url, string SigningSecret);

public readonly record struct PushoverConfig(string AppToken, string UserKey);
