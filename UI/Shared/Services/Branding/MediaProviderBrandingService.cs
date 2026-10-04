using Domain.Providers;

namespace UI.Shared.Services.Branding;

public class MediaProviderBrandingService
{
	private const string MutedTile = "var(--muted)";

	private const string OwnTile = "transparent";

	private const string RadarrAndSonarr = "color-mix(in oklch, #FFC230, #35C5F4)";

	public string SinkColor(SinkKind sink)
	{
		return sink switch
		{
			SinkKind.Plex => GetBrandingInfo(MediaProvider.Plex).BrandColor,
			SinkKind.Arr => RadarrAndSonarr,
		};
	}

	public BrandingInfo GetBrandingInfo(MediaProvider provider) =>
		provider switch
		{
			MediaProvider.Plex => new(
				provider,
				"Plex",
				"Plex",
				"#E5A00D",
				new BrandArt("plex-square.svg"),
				new BrandArt("plex-logo-light.svg", "plex-logo-dark.svg") { Scale = 1.15 },
				new BrandTile(MutedTile, new BrandArt("plex-square.svg"), 70)
			),
			MediaProvider.Tmdb => new(
				provider,
				"The Movie Database",
				"TMDb",
				"#01B4E4",
				new BrandArt("tmdb-square.svg"),
				new BrandArt("tmdb-logo.svg"),
				new BrandTile("#0D253F", new BrandArt("tmdb-logo.svg"), 86)
			),
			MediaProvider.Imdb => new(
				provider,
				"IMDb",
				"IMDb",
				"#F5DE50",
				new BrandArt("imdb-square.svg"),
				new BrandArt("imdb-logo.svg"),
				new BrandTile(OwnTile, new BrandArt("imdb-tile.svg"), 100)
			),
			MediaProvider.Tvdb => new(
				provider,
				"The TVDB",
				"TVDB",
				"#23A962",
				new BrandArt("tvdb-square.svg"),
				new BrandArt("tvdb-logo-light.png", "tvdb-logo-dark.png"),
				new BrandTile($"color-mix(in oklab, #23A962 16%, {MutedTile})", new BrandArt("tvdb-tile-light.svg", "tvdb-tile-dark.svg"), 82)
			),
			MediaProvider.Mdblist => new(
				provider,
				"Movie Database List",
				"MDBList",
				"#4284CA",
				new BrandArt("mdblist-square.svg"),
				new BrandArt("mdblist-logo-light.svg", "mdblist-logo-dark.svg") { Scale = 1.25 },
				new BrandTile(OwnTile, new BrandArt("mdblist-square.svg"), 100)
			),
			MediaProvider.Radarr => new(
				provider,
				"Radarr",
				"Radarr",
				"#FFC230",
				new BrandArt("radarr-square-light.svg", "radarr-square-dark.svg"),
				new BrandArt("radarr-square-light.svg", "radarr-square-dark.svg"),
				new BrandTile(MutedTile, new BrandArt("radarr-square-light.svg", "radarr-square-dark.svg"), 70)
			),
			MediaProvider.Sonarr => new(
				provider,
				"Sonarr",
				"Sonarr",
				"#35C5F4",
				new BrandArt("sonarr-square-light.svg", "sonarr-square-dark.svg"),
				new BrandArt("sonarr-square-light.svg", "sonarr-square-dark.svg"),
				new BrandTile(MutedTile, new BrandArt("sonarr-square-light.svg", "sonarr-square-dark.svg"), 70)
			),
		};
}
