using API.Plex;
using Contracts.Media.Artwork;

namespace API.Tests.Plex;

public class PlexUrlsTests
{
	private static PlexUrls Testee => PlexTestUrls.Instance;

	[Test]
	public async Task Search_ForATitle_AsksDiscoverWithTheTitleLast()
	{
		await Assert
			.That(Testee.Search("movies,tv", "Heat: 1995", 10))
			.IsEqualTo(
				"https://discover.provider.plex.tv/library/search?searchTypes=movies,tv&searchProviders=discover&includeMetadata=1&limit=10&query=Heat%3A%201995"
			);
	}

	[Test]
	public async Task Match_ForAGuid_EscapesIt()
	{
		await Assert
			.That(Testee.Match(1, "imdb://tt0035160"))
			.IsEqualTo("https://discover.provider.plex.tv/library/metadata/matches?type=1&guid=imdb%3A%2F%2Ftt0035160");
	}

	[Test]
	public async Task RatingKeyMetadata_ForAKey_AsksForGuidsAndExternalMedia()
	{
		await Assert
			.That(Testee.RatingKeyMetadata("5d776825880197001ec967c6"))
			.IsEqualTo("https://metadata.provider.plex.tv/library/metadata/5d776825880197001ec967c6?includeGuids=1&includeExternalMedia=1");
		await Assert
			.That(Testee.RatingKeyMetadata("a/b"))
			.IsEqualTo("https://metadata.provider.plex.tv/library/metadata/a%2Fb?includeGuids=1&includeExternalMedia=1");
	}

	[Test]
	public async Task Watchlist_AddAndRemove_CarryTheRatingKey()
	{
		await Assert.That(Testee.AddToWatchlist("5d77")).IsEqualTo("https://vod.provider.plex.tv/actions/addToWatchlist?ratingKey=5d77");
		await Assert.That(Testee.RemoveFromWatchlist("5d77")).IsEqualTo("https://vod.provider.plex.tv/actions/removeFromWatchlist?ratingKey=5d77");
	}

	[Test]
	public async Task Photo_ForAnArtworkUrl_EscapesItAfterTheSize()
	{
		await Assert
			.That(Testee.Photo("https://metadata-static.plex.tv/a/b.jpg?x=1", new ArtworkSize(300, 450)))
			.IsEqualTo("https://images.plex.tv/photo?width=300&height=450&minSize=1&url=https%3A%2F%2Fmetadata-static.plex.tv%2Fa%2Fb.jpg%3Fx%3D1");
	}
}
