namespace Shared.Options;

public record TraktOptions : IOptionsSection, IProviderHttpOptions
{
	public required string BaseUrl { get; set; }
	public required string UserAgent { get; set; }
	public ProviderHttpOptions Http { get; set; } = new();
	public IReadOnlyDictionary<string, string> Urls => new Dictionary<string, string>(StringComparer.Ordinal) { ["api"] = BaseUrl };
	public static string SectionName => "Trakt";
}
