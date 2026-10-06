using Domain.Providers;
using Domain.Wantlist.Arr;
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
			.. Enum.GetValues<ArrApp>().Select(app => (Func<BrandingInfo>)(() => branding.GetBrandingInfo(app))),
			.. Enum.GetValues<RemoteService>().Select(remote => (Func<BrandingInfo>)(() => branding.GetBrandingInfo(remote))),
			() => branding.GetBrandingInfo(AuthProvider.Local),
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
	public async Task GetBrandingInfo_ForThePlexSignIn_IsPlexsBrand()
	{
		await Assert.That(Testee.GetBrandingInfo(AuthProvider.Plex)).IsEqualTo(Testee.GetBrandingInfo(MediaProvider.Plex));
	}

	[Test]
	public async Task GetBrandingInfo_ForSammlarrAccounts_IsSammlarrsOwnBrand()
	{
		await Assert.That(Testee.GetBrandingInfo(AuthProvider.Local).NickName).IsEqualTo("Sammlarr");
	}

	[Test]
	public async Task SinkColor_ForPlex_IsPlexsBrand()
	{
		await Assert.That(Testee.SinkColor(SinkKind.Plex)).IsEqualTo(Testee.GetBrandingInfo(MediaProvider.Plex).BrandColor);
	}

	[Test]
	public async Task SinkColor_ForRadarrAndSonarr_MixesBothAppsBrands()
	{
		var radarr = Testee.GetBrandingInfo(ArrApp.Radarr).BrandColor;
		var sonarr = Testee.GetBrandingInfo(ArrApp.Sonarr).BrandColor;

		await Assert.That(Testee.ArrColor).IsEqualTo($"color-mix(in oklch, {radarr}, {sonarr})");
		await Assert.That(Testee.SinkColor(SinkKind.Arr)).IsEqualTo(Testee.ArrColor);
	}

	[Test]
	public async Task Brands_OfASink_AreTheBrandsBehindIt()
	{
		await Assert.That(Testee.Brands(SinkKind.Plex).Select(brand => brand.NickName)).IsEquivalentTo(["Plex"]);
		await Assert.That(Testee.Brands(SinkKind.Arr).Select(brand => brand.NickName)).IsEquivalentTo(["Radarr", "Sonarr"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Brands_OfASearch_AreTheBrandsBehindIt()
	{
		await Assert.That(Testee.Brands(SearchProvider.Tmdb).Select(brand => brand.NickName)).IsEquivalentTo(["TMDb"]);
		await Assert.That(Testee.Brands(SearchProvider.Plex).Select(brand => brand.NickName)).IsEquivalentTo(["Plex"]);
		await Assert.That(Testee.Brands(SearchProvider.Arr).Select(brand => brand.NickName)).IsEquivalentTo(["Radarr", "Sonarr"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task GetBrandingInfo_ForARemoteServiceWithItsOwnIds_IsTheBrandOfThoseIds()
	{
		var mismatched = Enum.GetValues<RemoteService>()
			.Where(remote => remote.OwnIds.Map(ids => Testee.GetBrandingInfo(ids) != Testee.GetBrandingInfo(remote)).GetValueOr(false));

		await Assert.That(mismatched).IsEmpty();
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
	public async Task Brands_OfAService_AreTheBrandsBehindIt()
	{
		await Assert.That(Testee.Brands(RemoteService.Plex).Select(brand => brand.NickName)).IsEquivalentTo(["Plex"]);
		await Assert.That(Testee.Brands(new Arr()).Select(brand => brand.NickName)).IsEquivalentTo(["Radarr", "Sonarr"], CollectionOrdering.Matching);
		await Assert.That(Testee.Brands(new SammlarrAccounts()).Select(brand => brand.NickName)).IsEquivalentTo(["Sammlarr"]);
	}

	[Test]
	public async Task Color_OfAServiceOfTwoApps_MixesTheirBrands()
	{
		await Assert.That(Testee.Color(new Arr())).IsEqualTo(Testee.ArrColor);
		await Assert.That(Testee.Color(RemoteService.Plex)).IsEqualTo(Testee.GetBrandingInfo(MediaProvider.Plex).BrandColor);
	}

	private static bool IsUnclean(string svg) => svg.Contains("<?xml") || svg.Contains("<!--") || svg.Contains("<title") || svg.Contains("<text");
}
