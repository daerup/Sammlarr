namespace Shared.Options;

public record MalOptions : IOptionsSection, IProviderHttpOptions
{
	public required string BaseUrl { get; set; }
	public ProviderHttpOptions Http { get; set; } = new();
	public static string SectionName => "MyAnimeList";
}
