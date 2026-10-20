using System.Diagnostics.CodeAnalysis;
using Domain.Providers;
using Domain.Wantlist.Arr;
using Humanizer;

namespace UI.Shared.Services.Branding;

public class MediaProviderBrandingService
{
	private const string MutedTile = "var(--muted)";

	private const string OwnTile = "transparent";

	private static readonly BrandingInfo Sammlarr = new(
		"Sammlarr accounts",
		"Sammlarr",
		"var(--foreground)",
		new BrandArt("sammlarr-square-light.svg", "sammlarr-square-dark.svg"),
		new BrandArt("sammlarr-square-light.svg", "sammlarr-square-dark.svg"),
		new BrandTile(MutedTile, new BrandArt("sammlarr-square-light.svg", "sammlarr-square-dark.svg"), 70)
	);

	public BrandingInfo GetBrandingInfo(RemoteService remote)
	{
		return remote switch
		{
			RemoteService.Tmdb => GetBrandingInfo(MediaProvider.Tmdb),
			RemoteService.Plex => GetBrandingInfo(MediaProvider.Plex),
			RemoteService.Mdblist => GetBrandingInfo(MediaProvider.Mdblist),
			RemoteService.Tvmaze => GetBrandingInfo(MediaProvider.Tvmaze),
			RemoteService.AniList => GetBrandingInfo(MediaProvider.AniList),
			RemoteService.Mal => GetBrandingInfo(MediaProvider.Mal),
			RemoteService.Trakt => GetBrandingInfo(MediaProvider.Trakt),
			RemoteService.ImdbDatasets => GetBrandingInfo(MediaProvider.Imdb),
			RemoteService.AnimeLists => new(
				"Anime lists",
				"Anime lists",
				"#7C6FCD",
				new BrandArt("github-mark-light.svg", "github-mark-dark.svg"),
				new BrandArt("anime-lists-logo-light.svg", "anime-lists-logo-dark.svg"),
				new BrandTile(MutedTile, new BrandArt("github-mark-light.svg", "github-mark-dark.svg"), 65)
			),
			RemoteService.Wikidata => new(
				"Wikidata",
				"Wikidata",
				"#006699",
				new BrandArt("wikidata-square.svg"),
				new BrandArt("wikidata-logo.svg") { Scale = 1.8 },
				new BrandTile(MutedTile, new BrandArt("wikidata-square.svg"), 70)
			),
			RemoteService.Radarr => new(
				"Radarr",
				"Radarr",
				"#FFC230",
				new BrandArt("radarr-square-light.svg", "radarr-square-dark.svg"),
				new BrandArt("radarr-square-light.svg", "radarr-square-dark.svg"),
				new BrandTile(MutedTile, new BrandArt("radarr-square-light.svg", "radarr-square-dark.svg"), 70)
			),
			RemoteService.Sonarr => new(
				"Sonarr",
				"Sonarr",
				"#35C5F4",
				new BrandArt("sonarr-square-light.svg", "sonarr-square-dark.svg"),
				new BrandArt("sonarr-square-light.svg", "sonarr-square-dark.svg"),
				new BrandTile(MutedTile, new BrandArt("sonarr-square-light.svg", "sonarr-square-dark.svg"), 70)
			),
		};
	}

	public IReadOnlyList<BrandingInfo> Brands(Service service)
	{
		return service switch
		{
			RemoteService remote => [GetBrandingInfo(remote)],
			Arr => [.. Enum.GetValues<ArrApp>().Select(app => GetBrandingInfo(app.Remote))],
			SammlarrAccounts => [Sammlarr],
		};
	}

	public string Color(Service service)
	{
		var colors = Brands(service).Select(brand => brand.BrandColor).ToList();
		return colors.Count == 1 ? colors[0] : $"color-mix(in oklch, {string.Join(", ", colors)})";
	}

	public string Name(Service service)
	{
		return service switch
		{
			RemoteService remote => Name(remote),
			Arr => string.Join(" + ", Enum.GetValues<ArrApp>().Select(app => Name(app.Remote))),
			SammlarrAccounts => Sammlarr.DisplayName,
		};
	}

	public string NickName(Service service) => string.Join(" + ", Brands(service).Select(brand => brand.NickName));

	public string Name(RemoteService remote) => remote.OwnIds.Map(provider => provider.Humanize()).GetValueOr(() => GetBrandingInfo(remote).NickName);

	[SuppressMessage(
		"Performance",
		"CA1822:Mark members as static",
		Justification = "the components call both GetBrandingInfo overloads on the injected service, so a static one would split the overload set"
	)]
	public BrandingInfo GetBrandingInfo(MediaProvider provider) =>
		provider switch
		{
			MediaProvider.Plex => new(
				"Plex",
				"Plex",
				"#E5A00D",
				new BrandArt("plex-square.svg"),
				new BrandArt("plex-logo-light.svg", "plex-logo-dark.svg") { Scale = 1.15 },
				new BrandTile(MutedTile, new BrandArt("plex-square.svg"), 70)
			),
			MediaProvider.Tmdb => new(
				"The Movie Database",
				"TMDb",
				"#01B4E4",
				new BrandArt("tmdb-square.svg"),
				new BrandArt("tmdb-logo.svg"),
				new BrandTile("#0D253F", new BrandArt("tmdb-logo.svg"), 86)
			),
			MediaProvider.Imdb => new(
				"IMDb",
				"IMDb",
				"#F5DE50",
				new BrandArt("imdb-square.svg"),
				new BrandArt("imdb-logo.svg"),
				new BrandTile(OwnTile, new BrandArt("imdb-tile.svg"), 100)
			),
			MediaProvider.RottenTomatoes => new(
				"Rotten Tomatoes®",
				"Rotten Tomatoes®",
				"#FA320A",
				new BrandArt("rottentomatoes-square.svg"),
				new BrandArt("rottentomatoes-logo.png"),
				new BrandTile(MutedTile, new BrandArt("rottentomatoes-square.svg"), 70)
			),
			MediaProvider.Tvdb => new(
				"The TVDB",
				"TVDB",
				"#23A962",
				new BrandArt("tvdb-square.svg"),
				new BrandArt("tvdb-logo-light.png", "tvdb-logo-dark.png"),
				new BrandTile($"color-mix(in oklab, #23A962 16%, {MutedTile})", new BrandArt("tvdb-tile-light.svg", "tvdb-tile-dark.svg"), 82)
			),
			MediaProvider.Mdblist => new(
				"Movie Database List",
				"MDBList",
				"#4284CA",
				new BrandArt("mdblist-square.svg"),
				new BrandArt("mdblist-logo-light.svg", "mdblist-logo-dark.svg") { Scale = 1.25 },
				new BrandTile(OwnTile, new BrandArt("mdblist-square.svg"), 100)
			),
			MediaProvider.Metacritic => new(
				"Metacritic",
				"Metacritic",
				"#FFBD3F",
				new BrandArt("metacritic-square.svg"),
				new BrandArt("metacritic-logo-light.svg", "metacritic-logo-dark.svg"),
				new BrandTile(MutedTile, new BrandArt("metacritic-square.svg"), 70)
			),
			MediaProvider.Letterboxd => new(
				"Letterboxd",
				"Letterboxd",
				"#00E054",
				new BrandArt("letterboxd-square.svg"),
				new BrandArt("letterboxd-logo-light.svg", "letterboxd-logo-dark.svg"),
				new BrandTile(MutedTile, new BrandArt("letterboxd-square.svg"), 70)
			),
			MediaProvider.Trakt => new(
				"Trakt",
				"Trakt",
				"#9F42C6",
				new BrandArt("trakt-square.svg"),
				new BrandArt("trakt-logo-light.svg", "trakt-logo-dark.svg"),
				new BrandTile(OwnTile, new BrandArt("trakt-square.svg"), 100)
			),
			MediaProvider.Mal => new(
				"MyAnimeList",
				"MyAnimeList",
				"#2F52A2",
				new BrandArt("myanimelist-square.svg"),
				new BrandArt("myanimelist-logo.png"),
				new BrandTile(OwnTile, new BrandArt("myanimelist-square.svg"), 100)
			),
			MediaProvider.Tvmaze => new(
				"TVmaze",
				"TVmaze",
				"#3C948B",
				new BrandArt("tvmaze-square-light.svg", "tvmaze-square-dark.svg"),
				new BrandArt("tvmaze-logo.png"),
				new BrandTile(MutedTile, new BrandArt("tvmaze-square-light.svg", "tvmaze-square-dark.svg"), 75)
			),
			MediaProvider.AniList => new(
				"AniList",
				"AniList",
				"#02A9FF",
				new BrandArt("anilist-square.png"),
				new BrandArt("anilist-logo-light.png", "anilist-logo-dark.png"),
				new BrandTile(OwnTile, new BrandArt("anilist-square.png"), 100)
			),
		};
}
