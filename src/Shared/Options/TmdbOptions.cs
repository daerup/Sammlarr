namespace Shared.Options;

public record TmdbOptions : IOptionsSection, IProviderHttpOptions
{
	public required string BaseUrl { get; set; }
	public required string V4BaseUrl { get; set; }
	public required string ImageBaseUrl { get; set; }
	public ProviderHttpOptions Http { get; set; } = new();
	public IReadOnlyDictionary<string, string> Urls => new Dictionary<string, string> { ["api"] = BaseUrl, ["v4"] = V4BaseUrl };
	public static string SectionName => "TMDB";
}
