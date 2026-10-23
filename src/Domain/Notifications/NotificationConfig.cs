namespace Domain.Notifications;

public readonly union NotificationConfig(WebhookConfig, PushoverConfig, WebUiConfig);

public readonly record struct WebhookConfig(Secret Url, Secret SigningSecret)
{
	public Optional<Uri> Endpoint()
	{
		return Optional.When(Uri.TryCreate(Url.Reveal().Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https", () => uri!);
	}
}

public readonly record struct PushoverConfig(Secret AppToken, Secret UserKey);

public readonly record struct WebUiConfig();
