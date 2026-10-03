using API.Tmdb;
using Contracts.Media.Artwork;
using Shared.Options;
using TestInfrastructure;

namespace API.Tests.TMDB;

public class TmdbArtworkTests
{
	private static TmdbArtwork Testee => new(new TmdbOptions { BaseUrl = "https://api.themoviedb.org/3", ImageBaseUrl = "https://images.example.test/t/p" });

	[Test]
	public async Task Original_WhenGivenATmdbPath_ComposesItUnderTheConfiguredBase()
	{
		var url = Testee.Original("/backdrop.jpg");

		await Assert.That(url).IsEqualTo("https://images.example.test/t/p/original/backdrop.jpg");
	}

	[Test]
	public async Task Original_WhenThePathIsMissing_IsNull()
	{
		var url = Testee.Original(null);

		await Assert.That(url).IsNull();
	}

	[Test]
	[Arguments(110, "w154")]
	[Arguments(780, "w780")]
	[Arguments(1000, "w1280")]
	public async Task Resize_WhenTheArtworkIsUnderTheConfiguredBase_PicksTheSmallestTmdbSizeThatCoversIt(int width, string expected)
	{
		var resized = Testee.Resize("https://images.example.test/t/p/original/backdrop.jpg", new ArtworkSize(width, width / 2));

		await Assert.That(resized).IsSome().And.IsEqualTo($"https://images.example.test/t/p/{expected}/backdrop.jpg");
	}

	[Test]
	public async Task Resize_WhenWiderThanTmdbsLargestSize_IsNone()
	{
		var resized = Testee.Resize("https://images.example.test/t/p/original/backdrop.jpg", new ArtworkSize(2560, 1355));

		await Assert.That(resized).IsNone();
	}

	[Test]
	[Arguments("https://image.tmdb.org/t/p/original/backdrop.jpg")]
	[Arguments("https://images.example.test/t/p/original")]
	public async Task Resize_WhenTheUrlIsNotArtworkUnderTheConfiguredBase_IsNone(string url)
	{
		var resized = Testee.Resize(url, new ArtworkSize(780, 413));

		await Assert.That(resized).IsNone();
	}
}
