using Domain.Providers;
using TestInfrastructure;
using UI.Shared.Services.Branding;

namespace UI.Tests.Branding;

public class MediaProviderBrandingServiceTests
{
	private static readonly string ProvidersFolder = RepositoryRoot.Resolve("UI", "wwwroot", "providers");

	private MediaProviderBrandingService Testee { get; } = new();

	public static IEnumerable<MediaProvider> AllProviders() => Enum.GetValues<MediaProvider>();

	[Test]
	[MethodDataSource(nameof(AllProviders))]
	public async Task GetBrandingInfo_ForEveryProvider_NamesFilesThatExistAndAreClean(MediaProvider provider)
	{
		var brand = Testee.GetBrandingInfo(provider);

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
	public async Task GetBrandingInfo_ForSammlarrAccounts_IsSammlarrsOwnBrandWithFilesThatExist()
	{
		var brand = Testee.GetBrandingInfo(AuthProvider.Local);

		await Assert.That(brand.NickName).IsEqualTo("Sammlarr");
		await Assert.That(new[] { brand.Mark.Light, brand.Mark.Dark }.Where(file => !File.Exists(Path.Combine(ProvidersFolder, file)))).IsEmpty();
	}

	[Test]
	public async Task SinkColor_ForPlex_IsPlexsBrand()
	{
		await Assert.That(Testee.SinkColor(SinkKind.Plex)).IsEqualTo(Testee.GetBrandingInfo(MediaProvider.Plex).BrandColor);
	}

	[Test]
	public async Task SinkColor_ForRadarrAndSonarr_MixesBothBrands()
	{
		var radarr = Testee.GetBrandingInfo(MediaProvider.Radarr).BrandColor;
		var sonarr = Testee.GetBrandingInfo(MediaProvider.Sonarr).BrandColor;

		await Assert.That(Testee.SinkColor(SinkKind.Arr)).IsEqualTo($"color-mix(in oklch, {radarr}, {sonarr})");
	}

	private static bool IsUnclean(string svg) => svg.Contains("<?xml") || svg.Contains("<!--") || svg.Contains("<title") || svg.Contains("<text");
}
