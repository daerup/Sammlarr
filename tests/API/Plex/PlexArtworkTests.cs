using API.Plex;
using Contracts.Media.Artwork;
using TestInfrastructure;

namespace API.Tests.Plex;

public class PlexArtworkTests
{
	private const string Art = "https://metadata-static.plex.tv/a/gracenote/art.jpg";

	private static PlexArtwork Testee => new(PlexTestUrls.Instance);

	[Test]
	public async Task Resize_WhenTheArtworkIsHostedByPlex_RoutesItThroughThePhotoTranscoder()
	{
		var resized = Testee.Resize(Art, new ArtworkSize(780, 413));

		await Assert
			.That(resized)
			.IsSome()
			.And.IsEqualTo("https://images.plex.tv/photo?width=780&height=413&minSize=1&url=https%3A%2F%2Fmetadata-static.plex.tv%2Fa%2Fgracenote%2Fart.jpg");
	}

	[Test]
	public async Task Resize_WhenWiderThanTheTranscoderDelivers_IsNone()
	{
		var resized = Testee.Resize(Art, new ArtworkSize(1280, 678));

		await Assert.That(resized).IsNone();
	}

	[Test]
	public async Task Resize_WhenTheUrlIsNotPlexArtwork_IsNone()
	{
		var resized = Testee.Resize("https://image.tmdb.org/t/p/original/backdrop.jpg", new ArtworkSize(780, 413));

		await Assert.That(resized).IsNone();
	}
}
