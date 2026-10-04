using API.Tvdb;
using Contracts.Media.Artwork;
using TestInfrastructure;

namespace API.Tests.TVDB;

public class TvdbArtworkTests
{
	private const string Poster = "https://artworks.thetvdb.com/banners/posters/81189-10.jpg";

	private TvdbArtwork Testee { get; } = new();

	[Test]
	public async Task Resize_ForATile_OffersTheThumbnail()
	{
		await Assert.That(Testee.Resize(Poster, new ArtworkSize(154, 231))).IsSome().And.IsEqualTo("https://artworks.thetvdb.com/banners/posters/81189-10_t.jpg");
	}

	[Test]
	public async Task Resize_WiderThanTheThumbnail_KeepsTheOriginal()
	{
		await Assert.That(Testee.Resize(Poster, new ArtworkSize(500, 750))).IsSome().And.IsEqualTo(Poster);
	}

	[Test]
	public async Task Resize_ForAnotherHost_Declines()
	{
		await Assert.That(Testee.Resize("https://image.tmdb.org/t/p/original/p.jpg", new ArtworkSize(154, 231))).IsNone();
	}
}
