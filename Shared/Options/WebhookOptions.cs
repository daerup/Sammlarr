namespace Shared.Options;

public record WebhookOptions : IOptionsSection, IProviderHttpOptions
{
	public ProviderHttpOptions Http { get; set; } = new() { RetryAttempts = 0 };

	public string[] PrivateHosts { get; set; } = [];
	public static string SectionName => "Webhook";
}
