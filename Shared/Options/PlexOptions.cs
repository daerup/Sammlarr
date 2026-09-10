namespace Shared.Options;

// ReSharper disable once ClassNeverInstantiated.Global
public record PlexOptions : IOptionsSection, IProviderHttpOptions
{
	public required string BaseUrl { get; set; }
	public string DiscoverUrl { get; set; } = "https://discover.provider.plex.tv";
	public string VodUrl { get; set; } = "https://vod.provider.plex.tv";
	public string MetadataUrl { get; set; } = "https://metadata.provider.plex.tv";
	public string AuthAppUrl { get; set; } = "https://app.plex.tv/auth";
	public required OAuthenticationOptions Authentication { get; set; }
	public ProviderHttpOptions Http { get; set; } = new();
	public static string SectionName => "Plex";
}
