namespace Shared.Options;

// ReSharper disable once ClassNeverInstantiated.Global
public record PlexOptions : IOptionsSection, IProviderHttpOptions
{
	public required string BaseUrl { get; set; }
	public string DiscoverUrl { get; set; } = "https://discover.provider.plex.tv";
	public string VodUrl { get; set; } = "https://vod.provider.plex.tv";
	public string MetadataUrl { get; set; } = "https://metadata.provider.plex.tv";
	public string ArtworkUrl { get; set; } = "https://metadata-static.plex.tv";
	public string ImagesUrl { get; set; } = "https://images.plex.tv";
	public string AuthAppUrl { get; set; } = "https://app.plex.tv/auth";
	public required OAuthenticationOptions Authentication { get; set; }
	public ProviderHttpOptions Http { get; set; } = new();
	public IReadOnlyDictionary<string, string> Urls =>
		new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["plex"] = BaseUrl,
			["discover"] = DiscoverUrl,
			["metadata"] = MetadataUrl,
			["vod"] = VodUrl,
		};
	public static string SectionName => "Plex";

	public static IReadOnlyList<string> UrlSettings { get; } =
	[
		.. typeof(PlexOptions)
			.GetProperties()
			.Where(property => property.PropertyType == typeof(string) && property.Name.EndsWith("Url", StringComparison.Ordinal))
			.Select(property => property.Name),
	];
}
