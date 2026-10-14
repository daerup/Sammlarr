namespace Shared.Options;

public record AnimeListsOptions : IOptionsSection, IProviderHttpOptions
{
	public required string BaseUrl { get; set; }
	public required string UserAgent { get; set; }
	public ProviderHttpOptions Http { get; set; } = new();
	public IReadOnlyDictionary<string, string> Urls => new Dictionary<string, string> { ["api"] = BaseUrl };
	public static string SectionName => "AnimeLists";
}
