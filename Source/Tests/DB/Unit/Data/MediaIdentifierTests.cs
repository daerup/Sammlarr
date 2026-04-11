using Contracts.Media;
using DB.Data.MediaGroup;

namespace DB.Tests.Unit.Data;

public class MediaIdentifierTests
{
	[Test]
	[Arguments(MediaProvider.Tmdb, "12345")]
	[Arguments(MediaProvider.Imdb, "tt1234567")]
	[Arguments(MediaProvider.Tvdb, "98765")]
	[Arguments(MediaProvider.Plex, "plex-123")]
	public async Task MediaIdentifierFrom_WhenMediaIdProvided_MapsIdentifier(MediaProvider source, string externalId)
	{
		var testee = CreateMediaId(source, externalId);

		var identifier = MediaIdentifier.From(testee);

		await Assert.That(identifier.Source).IsEqualTo(source);
		await Assert.That(identifier.ExternalId).IsEqualTo(externalId);
		await Assert.That(identifier.MediaType).IsEqualTo(MediaType.Movie);
		await Assert.That(identifier.ToMediaId()).IsEqualTo(testee);
	}

	[Test]
	[Arguments(MediaProvider.Tmdb, "12345")]
	[Arguments(MediaProvider.Imdb, "tt1234567")]
	[Arguments(MediaProvider.Tvdb, "98765")]
	[Arguments(MediaProvider.Plex, "plex-123")]
	public async Task ToMediaId_WhenIdentifierProvided_MapsBackToSameValueObject(MediaProvider source, string externalId)
	{
		var testee = new MediaIdentifier(source, externalId, MediaType.Movie);
		var expected = CreateMediaId(source, externalId);

		var mediaId = testee.ToMediaId();

		await Assert.That(mediaId.Source).IsEqualTo(source);
		await Assert.That(mediaId.Value).IsEqualTo(externalId);
		await Assert.That(mediaId).IsEqualTo(expected);
	}

	private static IMediaId CreateMediaId(MediaProvider source, string externalId)
	{
		return source switch
		{
			MediaProvider.Tmdb => TmdbId.From(externalId, MediaType.Movie),
			MediaProvider.Imdb => ImdbId.From(externalId, MediaType.Movie),
			MediaProvider.Tvdb => TvdbId.From(externalId, MediaType.Movie),
			MediaProvider.Plex => PlexId.From(externalId, MediaType.Movie),
		};
	}
}
