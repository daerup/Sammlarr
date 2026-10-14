namespace Shared.Options;

public record PushoverOptions : IOptionsSection, IProviderHttpOptions
{
	public required string BaseUrl { get; set; }
	public ProviderHttpOptions Http { get; set; } = new() { RetryAttempts = 0 };
	public IReadOnlyDictionary<string, string> Urls => new Dictionary<string, string>(StringComparer.Ordinal) { ["api"] = BaseUrl };
	public static string SectionName => "Pushover";
}
