namespace Domain.Notifications;

public union NotificationConfig(WebhookConfig, PushoverConfig, WebUiConfig);

public readonly record struct WebhookConfig(string Url, string SigningSecret);

public readonly record struct PushoverConfig(string AppToken, string UserKey);

public readonly record struct WebUiConfig();
