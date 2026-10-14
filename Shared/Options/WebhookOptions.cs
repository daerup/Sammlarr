namespace Shared.Options;

public record WebhookOptions : IOptionsSection, IProviderHttpOptions
{
	public ProviderHttpOptions Http { get; set; } = new() { RetryAttempts = 0 };
	public IReadOnlyDictionary<string, string> Urls => new Dictionary<string, string>();
	public static string SectionName => "Webhook";
}
