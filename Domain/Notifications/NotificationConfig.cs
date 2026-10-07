namespace Domain.Notifications;

public union NotificationConfig(WebhookConfig, PushoverConfig, WebUiConfig);

public readonly record struct WebhookConfig(string Url, string SigningSecret)
{
	public Optional<Uri> Endpoint()
	{
		return Optional.When(Uri.TryCreate(Url.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https", () => uri!);
	}
}

public readonly record struct PushoverConfig(string AppToken, string UserKey);

public readonly record struct WebUiConfig();
