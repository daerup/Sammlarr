namespace Shared.Options;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed record PlexOptions : IOptionsSection
{
	public static string SectionName => "Plex";

	public required string BaseUrl { get; set; }
	public required OAuthenticationOptions Authentication { get; set; }
}
