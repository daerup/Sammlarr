namespace Shared.Options;

public record PushoverOptions : IOptionsSection, IProviderHttpOptions
{
	public required string BaseUrl { get; set; }
	public ProviderHttpOptions Http { get; set; } = new() { RetryAttempts = 0 };
	public static string SectionName => "Pushover";
}
