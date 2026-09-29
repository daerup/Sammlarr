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

	private static bool IsUnclean(string svg) => svg.Contains("<?xml") || svg.Contains("<!--") || svg.Contains("<title") || svg.Contains("<text");
}
