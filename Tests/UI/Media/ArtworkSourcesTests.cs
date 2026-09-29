using Contracts.Media.Artwork;
using TestInfrastructure;

namespace UI.Tests.Media;

public class ArtworkSourcesTests
{
	private const string Art = "https://cdn.example.test/art.jpg";

	[Test]
	public async Task ByWidth_WhenAProviderResizesTheArtwork_OffersEachSizeAtItsWidth()
	{
		var testee = ArtworkStub.Sources(ArtworkStub.Resizing(), ArtworkStub.Declining());

		var srcSet = testee.ByWidth(Art, new ArtworkSize(780, 413), new ArtworkSize(1280, 678));

		await Assert.That(srcSet).IsEqualTo($"{Art}?width=780 780w, {Art}?width=1280 1280w");
	}

	[Test]
	public async Task ByWidth_WhenTheFirstProviderDeclines_AsksTheNextOne()
	{
		var testee = ArtworkStub.Sources(ArtworkStub.Declining(), ArtworkStub.Resizing());

		var srcSet = testee.ByWidth(Art, new ArtworkSize(780, 413));

		await Assert.That(srcSet).IsEqualTo($"{Art}?width=780 780w");
	}

	[Test]
	public async Task ByWidth_WhenTwoSizesShareACopy_OffersItOnce()
	{
		var resizer = IArtworkResizer.Mock();
		resizer.Resize(Any(), Any()).Returns(Some.Of("https://cdn.example.test/w780/art.jpg"));
		var testee = ArtworkStub.Sources(resizer.Object, ArtworkStub.Declining());

		var srcSet = testee.ByWidth(Art, new ArtworkSize(700, 371), new ArtworkSize(780, 413));

		await Assert.That(srcSet).IsEqualTo("https://cdn.example.test/w780/art.jpg 700w");
	}

	[Test]
	public async Task ByWidth_WhenEveryProviderDeclines_IsNull()
	{
		var testee = ArtworkStub.Sources(ArtworkStub.Declining(), ArtworkStub.Declining());

		var srcSet = testee.ByWidth(Art, new ArtworkSize(780, 413));

		await Assert.That(srcSet).IsNull();
	}

	[Test]
	public async Task ByDensity_WhenAProviderResizesTheArtwork_OffersOneAndTwoTimesTheSize()
	{
		var testee = ArtworkStub.Sizing();

		var srcSet = testee.ByDensity(Art, new ArtworkSize(110, 165));

		await Assert.That(srcSet).IsEqualTo($"{Art}?width=110 1x, {Art}?width=220 2x");
	}
}
