using Domain.Providers;
using TestInfrastructure;
using TUnit.Assertions.Enums;
using UI.Shared.Services.Branding;

namespace UI.Tests.Branding;

public class MediaProviderBrandingServiceTests
{
	private static readonly string ProvidersFolder = RepositoryRoot.Resolve("UI", "wwwroot", "providers");

	private MediaProviderBrandingService Testee { get; } = new();

	public static IEnumerable<Func<BrandingInfo>> AllBrands()
	{
		var branding = new MediaProviderBrandingService();
		return
		[
			.. Enum.GetValues<MediaProvider>().Select(provider => (Func<BrandingInfo>)(() => branding.GetBrandingInfo(provider))),
			.. Enum.GetValues<RemoteService>().Select(remote => (Func<BrandingInfo>)(() => branding.GetBrandingInfo(remote))),
			() => branding.Brands(new SammlarrAccounts()).Single(),
		];
	}

	[Test]
	[MethodDataSource(nameof(AllBrands))]
	public async Task GetBrandingInfo_ForEveryBrand_NamesFilesThatExistAndAreClean(BrandingInfo brand)
	{
		var files = new[] { brand.Mark, brand.Logo, brand.Tile.Art }
			.SelectMany(art => new[] { art.Light, art.Dark })
			.Distinct()
			.Select(file => Path.Combine(ProvidersFolder, file))
			.ToList();
		var missing = files.Where(file => !File.Exists(file));
		var unclean = files.Where(file => file.EndsWith(".svg") && File.Exists(file) && IsUnclean(File.ReadAllText(file)));
		await Assert.That(missing).IsEmpty();
		await Assert.That(unclean).IsEmpty();
	}

	[Test]
	public async Task GetBrandingInfo_ForARemoteServiceWithItsOwnIds_IsTheBrandOfThoseIds()
	{
		var mismatched = Enum.GetValues<RemoteService>()
			.Where(remote => remote.OwnIds.Map(ids => Testee.GetBrandingInfo(ids) != Testee.GetBrandingInfo(remote)).GetValueOr(false));

		await Assert.That(mismatched).IsEmpty();
	}

	[Test]
	public async Task GetBrandingInfo_ForRottenTomatoes_CarriesTheRegisteredMarkAndItsOwnWordmark()
	{
		var brand = Testee.GetBrandingInfo(MediaProvider.RottenTomatoes);

		await Assert.That(brand.DisplayName).IsEqualTo("Rotten Tomatoes®");
		await Assert.That(brand.Logo.Light).IsEqualTo("rottentomatoes-logo.png");
	}

	[Test]
	public async Task GetBrandingInfo_ForWikidata_IsWikimediasStackedLogoInBothThemes()
	{
		var logo = Testee.GetBrandingInfo(RemoteService.Wikidata).Logo;

		await Assert.That(logo.Light).IsEqualTo("wikidata-logo.svg");
		await Assert.That(logo.IsThemed).IsFalse();
	}

	[Test]
	public async Task Brands_OfThePlexSignIn_IsPlexsBrand()
	{
		await Assert.That(Testee.Brands(AuthProvider.Plex.Service)).IsEquivalentTo([Testee.GetBrandingInfo(MediaProvider.Plex)]);
	}

	[Test]
	public async Task Brands_OfSammlarrAccounts_IsSammlarrsOwnBrand()
	{
		await Assert.That(Testee.Brands(AuthProvider.Local.Service).Single().NickName).IsEqualTo("Sammlarr");
	}

	[Test]
	public async Task Brands_OfAService_AreTheBrandsBehindIt()
	{
		await Assert.That(Testee.Brands(SinkKind.Plex.Service).Select(brand => brand.NickName)).IsEquivalentTo(["Plex"]);
		await Assert.That(Testee.Brands(SearchProvider.Tmdb.Service).Select(brand => brand.NickName)).IsEquivalentTo(["TMDb"]);
		await Assert.That(Testee.Brands(new Arr()).Select(brand => brand.NickName)).IsEquivalentTo(["Radarr", "Sonarr"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Color_OfASingleService_IsItsBrand()
	{
		await Assert.That(Testee.Color(SinkKind.Plex.Service)).IsEqualTo(Testee.GetBrandingInfo(MediaProvider.Plex).BrandColor);
	}

	[Test]
	public async Task Color_OfRadarrAndSonarr_MixesBothAppsBrands()
	{
		var radarr = Testee.GetBrandingInfo(RemoteService.Radarr).BrandColor;
		var sonarr = Testee.GetBrandingInfo(RemoteService.Sonarr).BrandColor;

		await Assert.That(Testee.Color(new Arr())).IsEqualTo($"color-mix(in oklch, {radarr}, {sonarr})");
	}

	[Test]
	public async Task Name_OfEveryKindOfService_ReadsLikeTheChoice()
	{
		await Assert.That(Testee.Name(RemoteService.Tmdb)).IsEqualTo("TMDB");
		await Assert.That(Testee.Name(RemoteService.Mdblist)).IsEqualTo("MDBList");
		await Assert.That(Testee.Name(RemoteService.Wikidata)).IsEqualTo("Wikidata");
		await Assert.That(Testee.Name(new Arr())).IsEqualTo("Radarr + Sonarr");
		await Assert.That(Testee.Name(new SammlarrAccounts())).IsEqualTo("Sammlarr accounts");
	}

	[Test]
	public async Task NickName_OfAService_JoinsItsBrandsShortNames()
	{
		await Assert.That(Testee.NickName(RemoteService.Tmdb)).IsEqualTo("TMDb");
		await Assert.That(Testee.NickName(new Arr())).IsEqualTo("Radarr + Sonarr");
	}

	private static bool IsUnclean(string svg) => svg.Contains("<!--") || svg.Contains("<title") || svg.Contains("<text");
}
