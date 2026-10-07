namespace Shared.Options;

public record WebhookOptions : IOptionsSection, IProviderHttpOptions
{
	public ProviderHttpOptions Http { get; set; } = new() { RetryAttempts = 0 };
	public static string SectionName => "Webhook";
}
