namespace Shared.Options;

// ReSharper disable once ClassNeverInstantiated.Global
public record PlexOptions : IOptionsSection
{
	public required string BaseUrl { get; set; }
	public required OAuthenticationOptions Authentication { get; set; }
	public static string SectionName => "Plex";
}
