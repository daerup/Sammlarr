namespace Shared.Options;

// ReSharper disable once ClassNeverInstantiated.Global
public record PlexOptions : IOptionsSection, IProviderHttpOptions
{
	public required string BaseUrl { get; set; }
	public required OAuthenticationOptions Authentication { get; set; }
	public ProviderHttpOptions Http { get; set; } = new();
	public static string SectionName => "Plex";
}
