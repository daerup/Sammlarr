using Bunit;
using Domain.Providers;
using Domain.Wantlist.Arr;
using TUnit.Assertions.Enums;
using UI.Shared.Branding;
using UI.Shared.Services.Branding;

namespace UI.Tests.Branding;

public class ProviderTileTests : BunitContext
{
	private static readonly MediaProviderBrandingService Branding = new();

	[Before(Test)]
	public void Setup() => Services.AddProviderBranding();

	[Test]
	[Arguments(28, "20x20")]
	[Arguments(30, "21x21")]
	public async Task Tile_ForAMarkOnTheMutedTile_HoldsItAtSeventyPercent(int size, string art)
	{
		var cut = Render<ProviderTile>(parameters => parameters.Add(tile => tile.Provider, MediaProvider.Plex).Add(tile => tile.Size, size));

		var image = cut.Find(".provider-tile img");
		await Assert.That($"{image.GetAttribute("width")}x{image.GetAttribute("height")}").IsEqualTo(art);
	}

	[Test]
	public async Task Tile_ForABadgeLikeImdb_DrawsTheBadgeAsTheWholeTile()
	{
		var cut = Render<ProviderTile>(parameters => parameters.Add(tile => tile.Provider, MediaProvider.Imdb).Add(tile => tile.Size, 30));

		var image = cut.Find(".provider-tile img");
		await Assert.That(image.GetAttribute("src")).IsEqualTo("_content/UI/providers/imdb-tile.svg");
		await Assert.That($"{image.GetAttribute("width")}x{image.GetAttribute("height")}").IsEqualTo("30x30");
	}

	[Test]
	public async Task PairTile_ForRadarrAndSonarr_DrawsBothMarksAtEighteenTwentyEighthsOfTheTile()
	{
		var cut = Render<ProviderPairTile>(parameters =>
			parameters.Add(tile => tile.First, Branding.GetBrandingInfo(ArrApp.Radarr)).Add(tile => tile.Second, Branding.GetBrandingInfo(ArrApp.Sonarr))
		);

		var images = cut.FindAll(".provider-pair img.is-light");
		await Assert.That(images.Select(image => image.GetAttribute("alt") ?? string.Empty)).IsEquivalentTo(["Radarr", "Sonarr"], CollectionOrdering.Matching);
		await Assert.That(images.Select(image => image.GetAttribute("width") ?? string.Empty)).IsEquivalentTo(["18", "18"]);
	}

	[Test]
	public async Task Tile_ForTvdb_SwitchesItsArtWithTheTheme()
	{
		var cut = Render<ProviderTile>(parameters => parameters.Add(tile => tile.Provider, MediaProvider.Tvdb));

		var sources = cut.FindAll(".provider-tile img").Select(image => image.GetAttribute("src") ?? string.Empty);
		await Assert.That(sources).IsEquivalentTo(["_content/UI/providers/tvdb-tile-light.svg", "_content/UI/providers/tvdb-tile-dark.svg"]);
	}
}
